using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using RecipeHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using RecipeHub.Api.Services;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RecipeController : Controller
    {
        private const string RecipeCacheVersionKey = "recipes:cache:version";
        private const string EngagementCacheVersionKey = "recipes:engagement:cache:version";
        private static readonly ConcurrentDictionary<string, byte> RefreshInProgress = new ConcurrentDictionary<string, byte>();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RefreshAfter = TimeSpan.FromSeconds(10);

        private readonly ILogger<RecipeController> _logger;
        private readonly IRecipeService _recipeService;
        private readonly IMemoryCache _memoryCache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly RecipeHubContext _context;
        private readonly IRecipeNutritionEstimator _nutritionEstimator;
        private readonly IRecipeTranslationService _recipeTranslationService;
        private readonly IRecipeTranslationQueue _translationQueue;
        private readonly IRecipeGenerationService _recipeGenerationService;

        public RecipeController(ILogger<RecipeController> logger, IRecipeService recipeService, IMemoryCache memoryCache, IServiceScopeFactory scopeFactory, IHostEnvironment hostEnvironment, RecipeHubContext context, IRecipeNutritionEstimator nutritionEstimator, IRecipeTranslationService recipeTranslationService, IRecipeTranslationQueue translationQueue, IRecipeGenerationService recipeGenerationService)
        {
            _logger = logger;
            _recipeService = recipeService;
            _memoryCache = memoryCache;
            _scopeFactory = scopeFactory;
            _hostEnvironment = hostEnvironment;
            _context = context;
            _nutritionEstimator = nutritionEstimator;
            _recipeTranslationService = recipeTranslationService;
            _translationQueue = translationQueue;
            _recipeGenerationService = recipeGenerationService;
        }

        [HttpPost("estimate-nutrition")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> EstimateNutrition([FromBody] NutritionEstimateRequest request)
        {
            if (request?.Ingredients == null || request.Ingredients.Count == 0)
                return BadRequest("At least one ingredient is required.");

            return new OkObjectResult(await _nutritionEstimator.EstimateAsync(request));
        }

        [HttpPost("generate")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        [EnableRateLimiting("RecipeGeneration")]
        public async Task<IActionResult> Generate([FromBody] RecipeGenerationRequest request)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.Prompt) && (request.PantryItems == null || request.PantryItems.Count == 0)))
                return BadRequest("Provide a prompt or pantry items to generate a recipe.");

            return new OkObjectResult(await _recipeGenerationService.GenerateAsync(request));
        }

        [HttpPost("create")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> Create([FromBody] RecipeViewModel model)
        {
            if (model?.Image == null || string.IsNullOrWhiteSpace(model.Image.Url))
            {
                ModelState.AddModelError(nameof(model.Image), "An image is required.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await CanonicalizeNewIngredientsAsync(model.Ingredients, model.Language,
                ingredient => ingredient.Name, (ingredient, name) => ingredient.Name = name,
                ingredient => ingredient.Language, (ingredient, language) => ingredient.Language = language))
            {
                return StatusCode(503, "New ingredient names could not be converted to canonical English. Please try again.");
            }

            var recipe = await _recipeService.Create(model);
            if (recipe == null)
                return BadRequest("Failed to create recipe!");

            TriggerIngredientImageGeneration(model?.Ingredients?.Select(i => i?.Name));
            TriggerTranslationRefresh(recipe.Id);

            BumpRecipeCacheVersion();

            return new OkObjectResult(recipe);
        }

        [HttpPost("update")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> Update([FromBody] RecipeUpdateViewModel recipe)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!await CanonicalizeNewIngredientsAsync(recipe.Ingredients, recipe.Language,
                ingredient => ingredient.Name, (ingredient, name) => ingredient.Name = name,
                ingredient => ingredient.Language, (ingredient, language) => ingredient.Language = language))
            {
                return StatusCode(503, "New ingredient names could not be converted to canonical English. Please try again.");
            }

            var result = await _recipeService.Update(recipe);

            BumpRecipeCacheVersion();

            _logger.LogTrace("Recipe has been updated! Recipe: {@Recipe}", result);
            if (result != null)
                TriggerTranslationRefresh(result.Id);

            return new OkObjectResult(result);
        }

        [HttpPost("deleterecipeingredient")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> DeleteRecipeIngredient([FromBody] List<IngredientResponse> ingredients)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            foreach (var ingredient in ingredients)
            {
                var result = await _recipeService.DeleteRecipeIngredient(ingredient);

                if (result == false)
                    return NotFound("Ingredient could not be found!");
            }

            BumpRecipeCacheVersion();

            _logger.LogTrace("Ingredients have been deleted! Ingredients: {@Ingredients}", ingredients);

            return new OkResult();
        }

        [HttpPost("addingredients")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> AddIngredients([FromBody] List<IngredientResponse> ingredients, string title, string creator)
        {
            if (!await CanonicalizeNewIngredientsAsync(ingredients, "English",
                ingredient => ingredient.Name, (ingredient, name) => ingredient.Name = name,
                ingredient => ingredient.Language, (ingredient, language) => ingredient.Language = language))
            {
                return StatusCode(503, "New ingredient names could not be converted to canonical English. Please try again.");
            }

            var recipe = await _recipeService.AddIngredients(ingredients, title, creator);
            if (recipe == null)
                return BadRequest("Failed to add new ingredients to recipe!");

            TriggerIngredientImageGeneration(ingredients?.Select(i => i?.Name));

            BumpRecipeCacheVersion();

            return new OkObjectResult(recipe);
        }

        private async Task<bool> CanonicalizeNewIngredientsAsync<T>(
            IEnumerable<T> ingredients,
            string fallbackLanguage,
            Func<T, string> getName,
            Action<T, string> setName,
            Func<T, string> getLanguage,
            Action<T, string> setLanguage)
        {
            var submittedIngredients = (ingredients ?? Enumerable.Empty<T>())
                .Where(ingredient => !string.IsNullOrWhiteSpace(getName(ingredient)))
                .ToList();
            if (submittedIngredients.Count == 0) return true;

            var existingNames = await _context.Ingredients
                .AsNoTracking()
                .Select(ingredient => ingredient.Name)
                .ToListAsync();
            var canonicalByName = existingNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var unknownIngredients = new List<T>();

            foreach (var ingredient in submittedIngredients)
            {
                var submittedName = getName(ingredient).Trim();
                if (canonicalByName.TryGetValue(submittedName, out var existingName))
                {
                    setName(ingredient, existingName);
                    setLanguage(ingredient, "English");
                }
                else
                {
                    setName(ingredient, submittedName);
                    unknownIngredients.Add(ingredient);
                }
            }

            foreach (var languageGroup in unknownIngredients.GroupBy(ingredient =>
                         string.IsNullOrWhiteSpace(getLanguage(ingredient)) ? fallbackLanguage : getLanguage(ingredient),
                         StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(languageGroup.Key, "English", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ingredient in languageGroup)
                    {
                        setName(ingredient, getName(ingredient).Trim());
                        setLanguage(ingredient, "English");
                    }

                    continue;
                }

                var canonicalNames = await _recipeTranslationService.CanonicalizeIngredientNamesAsync(
                    languageGroup.Select(getName), languageGroup.Key);
                if (canonicalNames == null) return false;

                foreach (var ingredient in languageGroup)
                {
                    if (!canonicalNames.TryGetValue(getName(ingredient), out var canonicalName)) return false;
                    setName(ingredient, canonicalByName.TryGetValue(canonicalName, out var existingName) ? existingName : canonicalName);
                    setLanguage(ingredient, "English");
                }
            }

            return true;
        }

        private void TriggerIngredientImageGeneration(IEnumerable<string> ingredientNames)
        {
            var names = ingredientNames?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names == null || names.Count == 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var ingredientService = scope.ServiceProvider.GetRequiredService<IIngredientService>();

                    foreach (var ingredientName in names)
                    {
                        var ingredient = await ingredientService.GetByName(ingredientName);
                        if (ingredient?.Image?.Url == null)
                        {
                            var result = await ingredientService.RegenerateImage(ingredientName);
                            if (!result.Updated)
                            {
                                _logger.LogWarning("Background image generation skipped/failed for ingredient {IngredientName}. Error: {Error}", ingredientName, result.Error);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background ingredient image generation failed");
                }
            });
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] List<Guid> recipeIds)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            foreach (var id in recipeIds)
            {
                var result = await _recipeService.Delete(id);
            }

            BumpRecipeCacheVersion();

            _logger.LogTrace("Recipes have been deleted! RecipeIds: {@RecipeIds}", recipeIds);

            var cleanedRecipes = await _recipeService.GetAll();

            return new OkObjectResult(cleanedRecipes);
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get(string title, string creator)
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:get:{title?.Trim().ToLowerInvariant()}:{creator?.Trim().ToLowerInvariant()}:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out RecipeResponse cachedRecipe))
            {
                return new OkObjectResult(cachedRecipe);
            }

            var recipe = await _recipeService.Get(title, creator);
            if (recipe == null)
            {
                _logger.LogError("Failed to fetch recipe!");
                return new NotFoundObjectResult("Failed to fetch recipe!");
            }

            _memoryCache.Set(cacheKey, recipe, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            _logger.LogTrace("Recipe fetched! Recipe: {@Recipe}", recipe);
            return new OkObjectResult(recipe);
        }

        [HttpGet("getbyid/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var recipe = await GetCachedRecipeByIdAsync(id);

            if (recipe == null)
            {
                _logger.LogError("Failed to fetch recipe by id! Id: {RecipeId}", id);
                return new NotFoundObjectResult("Failed to fetch recipe!");
            }

            _logger.LogTrace("Recipe fetched by id! Recipe: {@Recipe}", recipe);
            return new OkObjectResult(recipe);
        }

        [HttpGet("getbyid/{id}/translation")]
        [EnableRateLimiting("RecipeTranslations")]
        public async Task<IActionResult> GetTranslation(Guid id, string language)
        {
            var requestTimer = Stopwatch.StartNew();
            var recipe = await GetCachedRecipeByIdAsync(id.ToString());
            var sourceLoadMilliseconds = requestTimer.ElapsedMilliseconds;
            if (recipe == null) return NotFound();

            if (string.IsNullOrWhiteSpace(language) ||
                language.Equals(recipe.Language, StringComparison.OrdinalIgnoreCase))
            {
                _translationQueue.EnqueueRemaining(new[] { id }, language);
                return Ok(recipe);
            }

            var translationCacheKey = $"recipes:translation:{id}:{language.ToLowerInvariant()}:v{recipe.LastUpdated?.Ticks.ToString() ?? "initial"}";
            if (_memoryCache.TryGetValue(translationCacheKey, out RecipeResponse cachedTranslation))
                return Ok(cachedTranslation);

            var storedTranslation = await _recipeTranslationService.GetStoredTranslationAsync(recipe, language);
            var translationLookupMilliseconds = requestTimer.ElapsedMilliseconds - sourceLoadMilliseconds;
            if (storedTranslation != null)
            {
                _memoryCache.Set(translationCacheKey, storedTranslation, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
                _logger.LogInformation("Recipe translation served from database. RecipeId: {RecipeId}; Language: {Language}; SourceLoadMs: {SourceLoadMs}; TranslationLookupMs: {TranslationLookupMs}; TotalMs: {TotalMs}", id, language, sourceLoadMilliseconds, translationLookupMilliseconds, requestTimer.ElapsedMilliseconds);
                return Ok(storedTranslation);
            }

            _logger.LogInformation("Recipe translation missing or stale. RecipeId: {RecipeId}; Language: {Language}; SourceLoadMs: {SourceLoadMs}; TranslationLookupMs: {TranslationLookupMs}", id, language, sourceLoadMilliseconds, translationLookupMilliseconds);
            var translatedRecipe = await _recipeTranslationService.TranslateAsync(recipe, language);
            var generationMilliseconds = requestTimer.ElapsedMilliseconds - sourceLoadMilliseconds - translationLookupMilliseconds;
            if (translatedRecipe == null || !string.Equals(translatedRecipe.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(503, new
                {
                    Code = "recipe_translation_unavailable",
                    Message = "This recipe is not available in the selected language yet."
                });
            }

            _memoryCache.Set(translationCacheKey, translatedRecipe, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });
            _translationQueue.EnqueueRemaining(new[] { id }, language);
            _logger.LogInformation("Recipe translation generated. RecipeId: {RecipeId}; Language: {Language}; SourceLoadMs: {SourceLoadMs}; TranslationLookupMs: {TranslationLookupMs}; GenerationMs: {GenerationMs}; TotalMs: {TotalMs}", id, language, sourceLoadMilliseconds, translationLookupMilliseconds, generationMilliseconds, requestTimer.ElapsedMilliseconds);
            return Ok(translatedRecipe);
        }

        private async Task<RecipeResponse> GetCachedRecipeByIdAsync(string id)
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:getbyid:{id}:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out RecipeResponse cachedRecipe))
                return cachedRecipe;

            RecipeResponse recipe;
            if (Guid.TryParse(id, out var recipeId))
            {
                recipe = await _recipeService.Get(recipeId);
            }
            else
            {
                var shortId = id?.Replace("-", string.Empty);
                recipe = await _recipeService.GetByShortId(shortId);
            }

            if (recipe != null)
            {
                _memoryCache.Set(cacheKey, recipe, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
            }

            return recipe;
        }

        [HttpGet("getallbycreator")]
        public async Task<IActionResult> GetAllByCreator(string creator)
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:getallbycreator:{creator?.ToLowerInvariant()}:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out RecipeCacheEntry cachedRecipes))
            {
                if (DateTimeOffset.UtcNow - cachedRecipes.RefreshedAt > RefreshAfter)
                {
                    TriggerBackgroundRefresh(cacheKey, async service => (await service.GetAll(creator)).ToList());
                }

                return new OkObjectResult(cachedRecipes.Data);
            }

            var recipes = (await _recipeService.GetAll(creator)).ToList();
            if (recipes == null)
            {
                _logger.LogError("Failed to fetch recipes!");
                return new NotFoundObjectResult("Failed to fetch recipes!");
            }

            _memoryCache.Set(cacheKey, new RecipeCacheEntry(recipes, DateTimeOffset.UtcNow), new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            _logger.LogTrace("Recipes fetched! Recipes: {@Recipes}", recipes);
            return new OkObjectResult(recipes);
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:getall:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out RecipeCacheEntry cachedRecipes))
            {
                if (DateTimeOffset.UtcNow - cachedRecipes.RefreshedAt > RefreshAfter)
                {
                    TriggerBackgroundRefresh(cacheKey, async service => (await service.GetAll()).ToList());
                }

                return new OkObjectResult(cachedRecipes.Data);
            }

            var recipes = (await _recipeService.GetAll()).ToList();
            if (recipes == null)
            {
                _logger.LogError("Failed to fetch recipes!");
                return new NotFoundObjectResult("Failed to fetch recipes!");
            }

            _logger.LogInformation("Recipe query returned {RecipeCount} recipes in {EnvironmentName}. Connection target is logged by the startup configuration.", recipes.Count, _hostEnvironment.EnvironmentName);

            var rawRecipeCount = await _context.Recipes.AsNoTracking().CountAsync();
            var includedRecipeCount = await _context.Recipes
                .AsNoTracking()
                .Include(recipe => recipe.Creator)
                .Include(recipe => recipe.Image)
                .CountAsync();
            _logger.LogInformation("Recipe diagnostics: raw table count={RawRecipeCount}; included query count={IncludedRecipeCount}.", rawRecipeCount, includedRecipeCount);

            _memoryCache.Set(cacheKey, new RecipeCacheEntry(recipes, DateTimeOffset.UtcNow), new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            _logger.LogTrace("Recipes fetched! Recipes: {@Recipes}", recipes);
            return new OkObjectResult(recipes);
        }

        [HttpGet("getallbyingredient")]
        public async Task<IActionResult> GetAllByIngredient(IngredientResponse ingredient)
        {
            var users = await _recipeService.GetAllByIngredient(ingredient);
            if (users == null)
            {
                _logger.LogError("Failed to fetch recipes by ingredient!");
                return new NotFoundObjectResult("Failed to fetch recipes by ingredient!");
            }

            _logger.LogTrace("Recipes fetched! Recipes: {@Recipes}", users);
            return new OkObjectResult(users);
        }

        [HttpGet("getallwithingredients")]
        public async Task<IActionResult> GetAllWithIngredients(string language = "English")
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:getallwithingredients:v{cacheVersion}";
            if (!_memoryCache.TryGetValue(cacheKey, out RecipeCacheEntry cachedRecipes))
            {
                var allRecipes = (await _recipeService.GetAllWithIngredients()).ToList();
                cachedRecipes = new RecipeCacheEntry(allRecipes, DateTimeOffset.UtcNow);
                _memoryCache.Set(cacheKey, cachedRecipes, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
            }

            var recipes = cachedRecipes.Data.ToList();

            if (!string.IsNullOrWhiteSpace(language) && !language.Equals("English", StringComparison.OrdinalIgnoreCase))
            {
                var storedTranslations = await _recipeTranslationService.GetAvailableStoredTranslationsAsync(recipes, language);
                if (storedTranslations.Count > 0)
                {
                    recipes = recipes.Select(item => storedTranslations.TryGetValue(item.Id, out var translated) ? translated : item).ToList();
                }

                if (recipes.Any(item => !language.Equals(item.Language, StringComparison.OrdinalIgnoreCase)))
                {
                    TriggerTranslationRefresh(recipes.Select(item => item.Id), language);
                }
            }

            return new OkObjectResult(recipes);
        }

        [HttpGet("paged")]
        [EnableRateLimiting("RecipeTranslations")]
        public async Task<IActionResult> GetPaged(
            int page = 1,
            int pageSize = 9,
            string search = null,
            string category = null,
            string tag = null,
            string sortBy = "created",
            bool ascending = false,
            string creator = null,
            string favoriteIds = null,
            string language = "English",
            bool? canBeFrozen = null)
        {
            try
            {
                page = page < 1 ? 1 : page;
                pageSize = pageSize < 1 ? 9 : pageSize;

                var pagedCacheKey = $"recipes:paged:{GetRecipeCacheVersion()}:{page}:{pageSize}:{search}:{category}:{tag}:{sortBy}:{ascending}:{creator}:{favoriteIds}:{language}:{canBeFrozen}";
                if (_memoryCache.TryGetValue(pagedCacheKey, out RecipePagedResponse cachedPage))
                    return Ok(cachedPage);

                var allRecipes = await GetAllRecipesCachedAsync();
                var engagementByRecipe = await GetRecipeEngagementStatsAsync();

                var availableCategories = allRecipes
                    .SelectMany(r => r.Categories ?? new List<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var availableTags = allRecipes
                    .SelectMany(r => r.Tags ?? new List<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                IEnumerable<RecipeResponse> filtered = allRecipes;

                if (!string.IsNullOrWhiteSpace(creator))
                {
                    filtered = filtered.Where(r => string.Equals(r.Creator, creator, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(favoriteIds))
                {
                    var ids = favoriteIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    filtered = filtered.Where(r => ids.Contains(r.Id.ToString()));
                }

                if (canBeFrozen.HasValue)
                {
                    filtered = filtered.Where(r => r.CanBeFrozen == canBeFrozen.Value);
                }

                if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
                {
                    var selectedCategories = SplitFilterValues(category);
                    filtered = filtered.Where(r => selectedCategories.All(selected =>
                        (r.Categories ?? new List<string>()).Any(value => !string.IsNullOrWhiteSpace(value) && string.Equals(value.Trim(), selected, StringComparison.OrdinalIgnoreCase))));
                }

                if (!string.IsNullOrWhiteSpace(tag) && !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
                {
                    var selectedTags = SplitFilterValues(tag);
                    filtered = filtered.Where(r => selectedTags.All(selected =>
                        (r.Tags ?? new List<string>()).Any(value => !string.IsNullOrWhiteSpace(value) && string.Equals(value.Trim(), selected, StringComparison.OrdinalIgnoreCase))));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    filtered = filtered.Where(r =>
                        Contains(r.Title, term) ||
                        Contains(r.Creator, term) ||
                        Contains(r.Description, term) ||
                        Contains(r.Instructions, term) ||
                        (r.Categories ?? new List<string>()).Any(c => Contains(c, term)) ||
                        (r.Tags ?? new List<string>()).Any(t => Contains(t, term)) ||
                        (r.Ingredients ?? new List<IngredientResponse>()).Any(i => Contains(i.Name, term)));
                }

                var sortComparer = GetCultureStringComparer(language);
                var isEnglish = string.IsNullOrWhiteSpace(language) || language.Equals("English", StringComparison.OrdinalIgnoreCase);
                var sortTitleTranslations = !isEnglish && string.Equals(sortBy, "title", StringComparison.OrdinalIgnoreCase)
                    ? await _recipeTranslationService.GetAvailableStoredTranslationsAsync(allRecipes.ToList(), language)
                    : new Dictionary<Guid, RecipeResponse>();
                if (sortTitleTranslations.Count < allRecipes.Count && !isEnglish && string.Equals(sortBy, "title", StringComparison.OrdinalIgnoreCase))
                {
                    TriggerTranslationRefresh(allRecipes.Where(r => !sortTitleTranslations.ContainsKey(r.Id)).Select(r => r.Id), language);
                }

                string GetSortTitle(RecipeResponse recipe) => sortTitleTranslations.TryGetValue(recipe.Id, out var translated) ? translated.Title : recipe.Title;

                filtered = sortBy?.ToLowerInvariant() switch
                {
                    "title" => ascending ? filtered.OrderBy(GetSortTitle, sortComparer) : filtered.OrderByDescending(GetSortTitle, sortComparer),
                    "creator" => ascending ? filtered.OrderBy(r => r.Creator, sortComparer) : filtered.OrderByDescending(r => r.Creator, sortComparer),
                    "time" => ascending ? filtered.OrderBy(GetTotalRecipeMinutes) : filtered.OrderByDescending(GetTotalRecipeMinutes),
                    "protein" => ascending
                        ? filtered.OrderBy(r => r.ProteinGrams.HasValue ? 0 : 1).ThenBy(r => r.ProteinGrams)
                        : filtered.OrderBy(r => r.ProteinGrams.HasValue ? 0 : 1).ThenByDescending(r => r.ProteinGrams),
                    "carbohydrates" => ascending
                        ? filtered.OrderBy(r => r.CarbohydrateGrams.HasValue ? 0 : 1).ThenBy(r => r.CarbohydrateGrams)
                        : filtered.OrderBy(r => r.CarbohydrateGrams.HasValue ? 0 : 1).ThenByDescending(r => r.CarbohydrateGrams),
                    "fiber" => ascending
                        ? filtered.OrderBy(r => r.FiberGrams.HasValue ? 0 : 1).ThenBy(r => r.FiberGrams)
                        : filtered.OrderBy(r => r.FiberGrams.HasValue ? 0 : 1).ThenByDescending(r => r.FiberGrams),
                    "rating" => ascending
                        ? filtered.OrderBy(r => GetEngagement(r.Id, engagementByRecipe).AverageRating.HasValue ? 0 : 1).ThenBy(r => GetEngagement(r.Id, engagementByRecipe).AverageRating).ThenByDescending(r => GetEngagement(r.Id, engagementByRecipe).RatingCount)
                        : filtered.OrderBy(r => GetEngagement(r.Id, engagementByRecipe).AverageRating.HasValue ? 0 : 1).ThenByDescending(r => GetEngagement(r.Id, engagementByRecipe).AverageRating).ThenByDescending(r => GetEngagement(r.Id, engagementByRecipe).RatingCount),
                    "popularity" => ascending
                        ? filtered.OrderBy(r => GetEngagement(r.Id, engagementByRecipe).MadeCount)
                        : filtered.OrderByDescending(r => GetEngagement(r.Id, engagementByRecipe).MadeCount),
                    _ => ascending ? filtered.OrderBy(r => r.Created) : filtered.OrderByDescending(r => r.Created),
                };

                var filteredList = filtered.ToList();
                var pageItems = filteredList
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(recipe => CreatePagedRecipeResponse(recipe, GetEngagement(recipe.Id, engagementByRecipe)))
                    .ToList();
                var storedPageTranslations = string.IsNullOrWhiteSpace(language) || language.Equals("English", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<Guid, RecipeResponse>()
                    : await _recipeTranslationService.GetAvailableStoredTranslationsAsync(pageItems, language);
                if (storedPageTranslations.Count > 0)
                {
                    pageItems = pageItems.Select(item => storedPageTranslations.TryGetValue(item.Id, out var translated) ? translated : item).ToList();
                }

                if (!string.IsNullOrWhiteSpace(language) && !language.Equals("English", StringComparison.OrdinalIgnoreCase) &&
                    pageItems.Any(item => !language.Equals(item.Language, StringComparison.OrdinalIgnoreCase)))
                {
                    TriggerTranslationRefresh(pageItems.Select(item => item.Id), language);
                }

                var response = new RecipePagedResponse
                {
                    Items = pageItems,
                    TotalCount = filteredList.Count,
                    Page = page,
                    PageSize = pageSize,
                    AvailableCategories = availableCategories,
                    AvailableTags = availableTags
                };

                _memoryCache.Set(pagedCacheKey, response, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });

                return new OkObjectResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch paged recipes. Page: {Page}, PageSize: {PageSize}", page, pageSize);
                return new ObjectResult("Unable to load recipes right now. Please try again.")
                {
                    StatusCode = 500
                };
            }
        }

        [HttpGet("engagement/{recipeId:guid}")]
        public async Task<IActionResult> GetEngagement(Guid recipeId)
        {
            if (!await _context.Recipes.AsNoTracking().AnyAsync(recipe => recipe.Id == recipeId))
                return NotFound();

            var stats = await GetRecipeEngagementAsync(recipeId, User?.Identity?.Name);
            return new OkObjectResult(stats);
        }

        [HttpPost("engagement")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> SaveEngagement([FromBody] RecipeEngagementViewModel model)
        {
            if (model == null || (model.Rating.HasValue && (model.Rating < 1 || model.Rating > 5)))
                return BadRequest("Rating must be between 1 and 5.");

            var userEmail = User?.Identity?.Name;
            var user = await _context.Users.SingleOrDefaultAsync(item => item.Email == userEmail);
            if (user == null)
                return Unauthorized();

            if (!await _context.Recipes.AnyAsync(recipe => recipe.Id == model.RecipeId))
                return NotFound();

            var engagement = await _context.RecipeRatings
                .SingleOrDefaultAsync(item => item.RecipeId == model.RecipeId && item.UserId == user.Id);

            if (engagement == null)
            {
                engagement = new Domain.Entities.Recipe.RecipeRating
                {
                    Id = Guid.NewGuid(),
                    RecipeId = model.RecipeId,
                    UserId = user.Id
                };
                _context.RecipeRatings.Add(engagement);
            }

            engagement.Rating = model.Rating;
            await _context.SaveChangesAsync();

            return new OkObjectResult(await GetRecipeEngagementAsync(model.RecipeId, user.Email));
        }

        [HttpGet("image/{recipeId:guid}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetRecipeImage(Guid recipeId)
        {
            var recipe = (await GetAllRecipesCachedAsync()).FirstOrDefault(item => item.Id == recipeId);
            var imageUrl = recipe?.Image?.Url;

            if (string.IsNullOrWhiteSpace(imageUrl))
                return NotFound();

            if (!IsDataUri(imageUrl))
                return Redirect(imageUrl);

            if (!TryDecodeDataUri(imageUrl, out var contentType, out var imageBytes))
                return NotFound();

            return File(imageBytes, contentType);
        }

        private static bool Contains(string value, string term)
        {
            return !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetTotalRecipeMinutes(RecipeResponse recipe)
        {
            return (recipe.PreparationMinutes ?? 0) +
                (recipe.CookingMinutes ?? 0) +
                (recipe.ProofingMinutes ?? 0) +
                (recipe.ChillingMinutes ?? 0) +
                (recipe.CoolingMinutes ?? 0) +
                (recipe.RestingMinutes ?? 0);
        }

        private static StringComparer GetCultureStringComparer(string language)
        {
            var cultureName = language?.Trim().ToLowerInvariant() switch
            {
                "danish" => "da-DK",
                "estonian" => "et-EE",
                "turkish" => "tr-TR",
                _ => "en-US"
            };

            return StringComparer.Create(CultureInfo.GetCultureInfo(cultureName), ignoreCase: true);
        }

        private static List<string> SplitFilterValues(string value)
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.Equals(item, "all", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private RecipeResponse CreatePagedRecipeResponse(RecipeResponse recipe, RecipeEngagementResponse engagement)
        {
            var image = recipe.Image == null
                ? null
                : new ImageResponse
                {
                    Id = recipe.Image.Id,
                    Caption = recipe.Image.Caption,
                    Url = IsDataUri(recipe.Image.Url)
                        ? $"{Request.Scheme}://{Request.Host}{Url.Action(nameof(GetRecipeImage), values: new { recipeId = recipe.Id, v = GetRecipeCacheVersion() })}"
                        : recipe.Image.Url
                };

            return new RecipeResponse
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Creator = recipe.Creator,
                Description = recipe.Description,
                Calories = recipe.Calories,
                ProteinGrams = recipe.ProteinGrams,
                CarbohydrateGrams = recipe.CarbohydrateGrams,
                FatGrams = recipe.FatGrams,
                FiberGrams = recipe.FiberGrams,
                SugarGrams = recipe.SugarGrams,
                SodiumMilligrams = recipe.SodiumMilligrams,
                MadeCount = engagement.MadeCount,
                AverageRating = engagement.AverageRating,
                RatingCount = engagement.RatingCount,
                Categories = recipe.Categories,
                Tags = recipe.Tags,
                Created = recipe.Created,
                LastUpdated = recipe.LastUpdated,
                Image = image
            };
        }

        private async Task<Dictionary<Guid, RecipeEngagementResponse>> GetRecipeEngagementStatsAsync()
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:engagement:{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out Dictionary<Guid, RecipeEngagementResponse> cachedStats))
                return cachedStats;

            var stats = await _context.RecipeRatings
                .AsNoTracking()
                .GroupBy(item => item.RecipeId)
                .Select(group => new
                {
                    RecipeId = group.Key,
                    MadeCount = group.Count(),
                    RatingCount = group.Count(item => item.Rating.HasValue),
                    AverageRating = group.Where(item => item.Rating.HasValue).Average(item => (decimal?)item.Rating)
                })
                .ToDictionaryAsync(item => item.RecipeId, item => new RecipeEngagementResponse
                {
                    MadeCount = item.MadeCount,
                    RatingCount = item.RatingCount,
                    AverageRating = item.AverageRating
                });

            _memoryCache.Set(cacheKey, stats, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });
            return stats;
        }

        private async Task<RecipeEngagementResponse> GetRecipeEngagementAsync(Guid recipeId, string userEmail)
        {
            var ratings = _context.RecipeRatings.AsNoTracking().Where(item => item.RecipeId == recipeId);
            var response = await ratings
                .GroupBy(item => item.RecipeId)
                .Select(group => new RecipeEngagementResponse
                {
                    MadeCount = group.Count(),
                    RatingCount = group.Count(item => item.Rating.HasValue),
                    AverageRating = group.Where(item => item.Rating.HasValue).Average(item => (decimal?)item.Rating)
                })
                .SingleOrDefaultAsync() ?? new RecipeEngagementResponse();

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var userRating = await ratings
                    .Where(item => item.User.Email == userEmail)
                    .Select(item => new { item.Rating })
                    .SingleOrDefaultAsync();
                response.HasMade = userRating != null;
                response.UserRating = userRating?.Rating;
            }

            return response;
        }

        private static RecipeEngagementResponse GetEngagement(Guid recipeId, IReadOnlyDictionary<Guid, RecipeEngagementResponse> engagementByRecipe)
        {
            return engagementByRecipe.TryGetValue(recipeId, out var engagement)
                ? engagement
                : new RecipeEngagementResponse();
        }

        private static bool IsDataUri(string value)
        {
            return value?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool TryDecodeDataUri(string value, out string contentType, out byte[] bytes)
        {
            contentType = null;
            bytes = null;

            if (!IsDataUri(value))
                return false;

            var separatorIndex = value.IndexOf(',');
            if (separatorIndex < 0)
                return false;

            var metadata = value.Substring(5, separatorIndex - 5);
            if (!metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                return false;

            contentType = metadata.Substring(0, metadata.Length - ";base64".Length);

            try
            {
                bytes = Convert.FromBase64String(value.Substring(separatorIndex + 1));
                return !string.IsNullOrWhiteSpace(contentType);
            }
            catch (FormatException)
            {
                contentType = null;
                bytes = null;
                return false;
            }
        }

        private async Task<List<RecipeResponse>> GetAllRecipesCachedAsync()
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:getall:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out RecipeCacheEntry cachedRecipes))
            {
                if (DateTimeOffset.UtcNow - cachedRecipes.RefreshedAt > RefreshAfter)
                {
                    TriggerBackgroundRefresh(cacheKey, async service => (await service.GetAll()).ToList());
                }

                return cachedRecipes.Data;
            }

            var recipes = (await _recipeService.GetAll()).ToList();
            _memoryCache.Set(cacheKey, new RecipeCacheEntry(recipes, DateTimeOffset.UtcNow), new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            return recipes;
        }

        private int GetRecipeCacheVersion()
        {
            if (_memoryCache.TryGetValue(RecipeCacheVersionKey, out int version))
            {
                return version;
            }

            _memoryCache.Set(RecipeCacheVersionKey, 0);
            return 0;
        }

        private void BumpRecipeCacheVersion()
        {
            var nextVersion = GetRecipeCacheVersion() + 1;
            _memoryCache.Set(RecipeCacheVersionKey, nextVersion);
        }

        private void TriggerBackgroundRefresh(string cacheKey, Func<IRecipeService, Task<List<RecipeResponse>>> refreshFactory)
        {
            if (!RefreshInProgress.TryAdd(cacheKey, 0))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var recipeService = scope.ServiceProvider.GetRequiredService<IRecipeService>();
                    var refreshedData = await refreshFactory(recipeService);

                    _memoryCache.Set(cacheKey, new RecipeCacheEntry(refreshedData, DateTimeOffset.UtcNow), new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheTtl
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background recipe cache refresh failed for key {CacheKey}", cacheKey);
                }
                finally
                {
                    RefreshInProgress.TryRemove(cacheKey, out _);
                }
            });
        }

        private void TriggerTranslationRefresh(Guid recipeId)
        {
            TriggerTranslationRefresh(new[] { recipeId });
        }

        private void TriggerTranslationRefresh(IEnumerable<Guid> recipeIds, string language = null)
        {
            _translationQueue.Enqueue(recipeIds, language);
        }

        private sealed class RecipeCacheEntry
        {
            public RecipeCacheEntry(List<RecipeResponse> data, DateTimeOffset refreshedAt)
            {
                Data = data;
                RefreshedAt = refreshedAt;
            }

            public List<RecipeResponse> Data { get; }
            public DateTimeOffset RefreshedAt { get; }
        }
    }
}

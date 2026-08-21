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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RecipeController : Controller
    {
        private const string RecipeCacheVersionKey = "recipes:cache:version";
        private static readonly ConcurrentDictionary<string, byte> RefreshInProgress = new ConcurrentDictionary<string, byte>();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RefreshAfter = TimeSpan.FromSeconds(10);

        private readonly ILogger<RecipeController> _logger;
        private readonly IRecipeService _recipeService;
        private readonly IMemoryCache _memoryCache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly RecipeHubContext _context;

        public RecipeController(ILogger<RecipeController> logger, IRecipeService recipeService, IMemoryCache memoryCache, IServiceScopeFactory scopeFactory, IHostEnvironment hostEnvironment, RecipeHubContext context)
        {
            _logger = logger;
            _recipeService = recipeService;
            _memoryCache = memoryCache;
            _scopeFactory = scopeFactory;
            _hostEnvironment = hostEnvironment;
            _context = context;
        }

        [HttpPost("create")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> Create([FromBody] RecipeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var recipe = await _recipeService.Create(model);
            if (recipe == null)
                return BadRequest("Failed to create recipe!");

            TriggerIngredientImageGeneration(model?.Ingredients?.Select(i => i?.Name));

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

            var result = await _recipeService.Update(recipe);

            BumpRecipeCacheVersion();

            _logger.LogTrace("Recipe has been updated! Recipe: {@Recipe}", result);

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
            var recipe = await _recipeService.AddIngredients(ingredients, title, creator);
            if (recipe == null)
                return BadRequest("Failed to add new ingredients to recipe!");

            TriggerIngredientImageGeneration(ingredients?.Select(i => i?.Name));

            BumpRecipeCacheVersion();

            return new OkObjectResult(recipe);
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
        public async Task<IActionResult> GetById(Guid id)
        {
            var cacheVersion = GetRecipeCacheVersion();
            var cacheKey = $"recipes:getbyid:{id}:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out RecipeResponse cachedRecipe))
            {
                return new OkObjectResult(cachedRecipe);
            }

            var recipe = await _recipeService.Get(id);
            if (recipe == null)
            {
                _logger.LogError("Failed to fetch recipe by id! Id: {RecipeId}", id);
                return new NotFoundObjectResult("Failed to fetch recipe!");
            }

            _memoryCache.Set(cacheKey, recipe, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            _logger.LogTrace("Recipe fetched by id! Recipe: {@Recipe}", recipe);
            return new OkObjectResult(recipe);
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

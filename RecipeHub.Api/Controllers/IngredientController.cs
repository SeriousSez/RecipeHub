using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using RecipeHub.Api.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [Route("[controller]")]
    public class IngredientController : Controller
    {
        private readonly ILogger<IngredientController> _logger;
        private readonly IIngredientService _ingredientService;
        private readonly IImageService _imageService;
        private readonly IRecipeTranslationService _recipeTranslationService;

        public IngredientController(ILogger<IngredientController> logger, IIngredientService ingredientService, IImageService imageService, IRecipeTranslationService recipeTranslationService)
        {
            _logger = logger;
            _ingredientService = ingredientService;
            _imageService = imageService;
            _recipeTranslationService = recipeTranslationService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] IngredientViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ingredient = await _ingredientService.Create(model);
            if (ingredient == null)
                return BadRequest("Failed to create Ingredient!");

            if (model.Image != null)
                await _imageService.Create(model.Image);

            return new OkResult();
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            var ingredients = await _ingredientService.GetAll();
            if (ingredients == null)
            {
                _logger.LogError("Failed to fetch Ingredients!");
                return new NotFoundObjectResult("Failed to fetch Ingredients!");
            }

            _logger.LogTrace("Ingredients fetched! Ingredients: {@Ingredients}", ingredients);
            return new OkObjectResult(ingredients);
        }

        [HttpGet("getalllite")]
        public async Task<IActionResult> GetAllLite(string language = "English")
        {
            var ingredients = (await _ingredientService.GetAllLite())?.ToList();
            if (ingredients == null)
            {
                _logger.LogError("Failed to fetch Ingredients (lite)!");
                return new NotFoundObjectResult("Failed to fetch Ingredients!");
            }

            var translatedNames = await _recipeTranslationService.TranslateIngredientNamesAsync(
                ingredients.Select(ingredient => ingredient.Name), language);
            foreach (var ingredient in ingredients)
            {
                var canonicalName = ingredient.Name?.Trim();
                ingredient.DisplayName = translatedNames != null && translatedNames.TryGetValue(canonicalName, out var displayName)
                    ? displayName
                    : canonicalName ?? ingredient.Name;
            }

            _logger.LogTrace("Ingredients fetched (lite)! Ingredients: {@Ingredients}", ingredients);
            return new OkObjectResult(ingredients);
        }

        [HttpPost("translate")]
        public async Task<IActionResult> Translate([FromBody] IngredientTranslationRequest request)
        {
            if (request?.Names == null || request.Names.Count == 0 || request.Names.Count > 200 ||
                request.Names.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 200))
            {
                return BadRequest();
            }

            var translations = await _recipeTranslationService.TranslateIngredientNamesAsync(request.Names, request.Language, request.Contexts);
            return new OkObjectResult(translations ?? new Dictionary<string, string>());
        }

        [HttpPost("updatetranslation")]
        public async Task<IActionResult> UpdateTranslation([FromBody] IngredientTranslationUpdateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IngredientName) ||
                string.IsNullOrWhiteSpace(request.Language) || string.IsNullOrWhiteSpace(request.TranslatedName))
            {
                return BadRequest();
            }

            await _recipeTranslationService.SaveIngredientTranslationAsync(
                request.IngredientName, request.Language, request.TranslatedName);
            return Ok();
        }

        [HttpGet("translations")]
        public async Task<IActionResult> GetTranslations(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest();
            return Ok(await _recipeTranslationService.GetIngredientTranslationsAsync(name));
        }

        [HttpGet("getbyname")]
        public async Task<IActionResult> GetByName(string name)
        {
            var ingredient = await _ingredientService.GetByName(name);
            if (ingredient == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(ingredient);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] IngredientResponse ingredient)
        {
            if (!ModelState.IsValid || ingredient == null || ingredient.Id == System.Guid.Empty)
            {
                return BadRequest(ModelState);
            }

            var result = await _ingredientService.Update(ingredient);
            if (result == null) return NotFound();

            _logger.LogTrace("Ingredient has been updated! Ingredient: {@Ingredient}", result);

            return new OkObjectResult(result);
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] List<IngredientResponse> ingredients)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            foreach (var ingredient in ingredients)
            {
                var deletedIngredient = await _ingredientService.Delete(ingredient);
                if (deletedIngredient == null)
                    return NotFound();
            }

            _logger.LogTrace("Ingredients have been deleted! Ingredients: {@Ingredients}", ingredients);

            var cleanedUsers = await _ingredientService.GetAll();

            return new OkObjectResult(cleanedUsers);
        }

        [HttpPost("regenerateimages")]
        public async Task<IActionResult> RegenerateImages([FromQuery] string[] excludeNames = null)
        {
            var (updated, skipped, failed, failedNames) = await _ingredientService.RegenerateImages(excludeNames);

            _logger.LogInformation("Ingredient image regeneration completed. Updated: {Updated}, Skipped: {Skipped}, Failed: {Failed}", updated, skipped, failed);

            return new OkObjectResult(new
            {
                Updated = updated,
                Skipped = skipped,
                Failed = failed,
                FailedNames = failedNames
            });
        }

        [HttpPost("regenerateimage")]
        public async Task<IActionResult> RegenerateImage([FromQuery] string name)
        {
            var (updated, error, ingredient) = await _ingredientService.RegenerateImage(name);
            if (!updated)
            {
                if (!string.IsNullOrWhiteSpace(error) && error.Contains("was not found"))
                {
                    return new NotFoundObjectResult(new { Updated = false, Error = error });
                }

                return new BadRequestObjectResult(new { Updated = false, Error = error });
            }

            return new OkObjectResult(new
            {
                Updated = true,
                Ingredient = ingredient
            });
        }
    }
}

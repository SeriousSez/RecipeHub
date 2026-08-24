using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using RecipeHub.Api.Services;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Models;
using System;
using RecipeHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class GroceryController : Controller
    {
        private readonly ILogger<GroceryController> _logger;
        private readonly IGroceryService _groceryService;
        private readonly IGroceryOfferService _groceryOfferService;
        private readonly RecipeHubContext _context;

        public GroceryController(ILogger<GroceryController> logger, IGroceryService groceryService, IGroceryOfferService groceryOfferService, RecipeHubContext context)
        {
            _logger = logger;
            _groceryService = groceryService;
            _groceryOfferService = groceryOfferService;
            _context = context;
        }

        [HttpPost("categoryfeedback")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveCategoryFeedback([FromBody] GroceryCategoryFeedbackViewModel model)
        {
            if (model == null || (model.Rating != -1 && model.Rating != 1) || string.IsNullOrWhiteSpace(model.IngredientName) || string.IsNullOrWhiteSpace(model.Category) ||
                model.IngredientName.Length > 100 || model.Category.Length > 200)
            {
                return BadRequest(new { code = "invalid_feedback" });
            }

            var ingredientName = NormalizeFeedbackValue(model.IngredientName);
            var category = NormalizeFeedbackValue(model.Category);
            var feedback = await _context.GroceryCategoryFeedback
                .SingleOrDefaultAsync(item => item.IngredientName == ingredientName && item.Category == category);

            if (feedback == null)
            {
                feedback = new Domain.Entities.Grocery.GroceryCategoryFeedback
                {
                    Id = Guid.NewGuid(),
                    IngredientName = ingredientName,
                    Category = category,
                    ApprovalCount = model.Rating == 1 ? 1 : 0,
                    RejectionCount = model.Rating == -1 ? 1 : 0
                };
                _context.GroceryCategoryFeedback.Add(feedback);
            }
            else
            {
                if (model.Rating == 1) feedback.ApprovalCount++;
                else feedback.RejectionCount++;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        private static string NormalizeFeedbackValue(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        [AllowAnonymous]
        [EnableRateLimiting("GroceryOffers")]
        [HttpPost("nearbyoffers")]
        public async Task<IActionResult> FindNearbyOffers([FromBody] GroceryOfferSearchViewModel model)
        {
            if (model?.IngredientNames == null || model.IngredientNames.Count == 0 || model.IngredientNames.Count > 50 ||
                model.IngredientNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 100) ||
                model.Latitude < -90 || model.Latitude > 90 || model.Longitude < -180 || model.Longitude > 180 ||
                model.RadiusKm <= 0 || model.RadiusKm > 50)
            {
                return BadRequest(new { code = "invalid_request" });
            }

            if (!_groceryOfferService.IsConfigured(model))
            {
                return StatusCode(503, new { code = "grocery_provider_not_configured" });
            }

            try
            {
                return Ok(await _groceryOfferService.FindNearbyOffersAsync(model));
            }
            catch (GroceryOfferProviderException exception)
            {
                _logger.LogWarning("Grocery offer search failed with upstream status {StatusCode}", exception.StatusCode);
                return StatusCode(503, new { code = exception.StatusCode == 429 ? "grocery_provider_rate_limited" : "grocery_provider_unavailable" });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Grocery offer search failed");
                return StatusCode(503, new { code = "grocery_provider_unavailable" });
            }
        }

        [HttpPost("createplan")]
        public async Task<IActionResult> CreatePlan([FromBody] GroceryListViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _groceryService.Create(model.UserId);

            return new OkResult();
        }

        [HttpPost("creategrocerylist")]
        public async Task<IActionResult> CreateGroceryList([FromBody] GroceryListViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _groceryService.Create(model.UserId);

            return new OkResult();
        }

        [HttpGet("getgrocerylists")]
        public async Task<IActionResult> GetGroceryLists(string userId)
        {
            var groceryList = await _groceryService.GetGroceryList(userId);
            if (groceryList == null)
            {
                _logger.LogError("Failed to fetch GroceryList!");
                return new NotFoundObjectResult("Failed to fetch GroceryList!");
            }

            _logger.LogTrace("GroceryList fetched! GroceryList: {@GroceryList}", groceryList);
            return new OkObjectResult(groceryList);
        }
    }
}

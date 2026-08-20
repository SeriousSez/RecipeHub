using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Models;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class GroceryController : Controller
    {
        private readonly ILogger<GroceryController> _logger;
        private readonly IGroceryService _groceryService;

        public GroceryController(ILogger<GroceryController> logger, IGroceryService groceryService)
        {
            _logger = logger;
            _groceryService = groceryService;
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

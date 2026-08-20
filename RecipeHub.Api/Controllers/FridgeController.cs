using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Models;
using System;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class FridgeController
    {
        private readonly ILogger<GroceryController> _logger;
        private readonly IFridgeService _fridgeService;

        public FridgeController(ILogger<GroceryController> logger, IFridgeService fridgeService)
        {
            _logger = logger;
            _fridgeService = fridgeService;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get(Guid userId)
        {
            var fridges = await _fridgeService.Get(userId);
            if (fridges == null)
            {
                _logger.LogError("Failed to fetch Fridges!");
                return new NotFoundObjectResult("Failed to fetch Fridges!");
            }

            _logger.LogTrace("Fridges fetched! Fridges: {@Fridges}", fridges);
            return new OkObjectResult(fridges);
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] FridgeModel model)
        {
            await _fridgeService.Add(model);

            return new OkResult();
        }

        [HttpPut("retire")]
        public async Task<IActionResult> Retire(Guid fridgeId)
        {
            await _fridgeService.Retire(fridgeId);

            return new OkResult();
        }

        [HttpPut("unretire")]
        public async Task<IActionResult> UnRetire(Guid fridgeId)
        {
            await _fridgeService.UnRetire(fridgeId);

            return new OkResult();
        }

        [HttpGet("getgroceries")]
        public async Task<IActionResult> GetGroceries(Guid fridgeId)
        {
            var groceries = await _fridgeService.GetGroceries(fridgeId);
            if (groceries == null)
            {
                _logger.LogError("Failed to fetch Groceries!");
                return new NotFoundObjectResult("Failed to fetch Groceries!");
            }

            _logger.LogTrace("Groceries fetched! Groceries: {@Groceries}", groceries);
            return new OkObjectResult(groceries);
        }

        [HttpPost("addgrocery")]
        public async Task<IActionResult> AddGrocery([FromBody] FridgeGroceryModel model)
        {
            await _fridgeService.AddGrocery(model);

            return new OkResult();
        }

        [HttpPost("removegrocery")]
        public async Task<IActionResult> RemoveGrocery(Guid id)
        {
            await _fridgeService.RemoveGrocery(id);

            return new OkResult();
        }
    }
}

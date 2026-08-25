using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Models;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class FoodPlanController : Controller
    {
        private readonly IFoodPlanService _foodPlanService;
        private readonly IUserRepository _userRepository;

        public FoodPlanController(IFoodPlanService foodPlanService, IUserRepository userRepository)
        {
            _foodPlanService = foodPlanService;
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Get(DateTime start, DateTime end, string userId = null)
        {
            var resolvedUserId = await ResolveUserId(userId);
            if (resolvedUserId == null) return Unauthorized();
            if (end.Date < start.Date) return BadRequest("End date must be on or after start date.");

            return Ok(await _foodPlanService.Get(resolvedUserId, start, end));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FoodPlanEntryViewModel model)
        {
            if (!IsValid(model, out var validationError)) return BadRequest(validationError);

            var resolvedUserId = await ResolveUserId(model.UserId);
            if (resolvedUserId == null) return Unauthorized();

            var created = await _foodPlanService.Create(resolvedUserId, model);
            if (created == null) return BadRequest("User or recipe could not be found.");

            return Ok(created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FoodPlanEntryViewModel model)
        {
            if (!IsValid(model, out var validationError)) return BadRequest(validationError);

            var resolvedUserId = await ResolveUserId(model.UserId);
            if (resolvedUserId == null) return Unauthorized();

            model.Id = id;
            var updated = await _foodPlanService.Update(resolvedUserId, model);
            if (updated == null) return NotFound("Food plan entry could not be found.");

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, string userId = null)
        {
            var resolvedUserId = await ResolveUserId(userId);
            if (resolvedUserId == null) return Unauthorized();

            return await _foodPlanService.Delete(resolvedUserId, id) ? Ok() : NotFound();
        }

        private static bool IsValid(FoodPlanEntryViewModel model, out string error)
        {
            error = null;
            if (model == null)
            {
                error = "A food plan entry is required.";
                return false;
            }

            if (model.RecipeId == Guid.Empty)
            {
                error = "A recipe is required.";
                return false;
            }

            if (model.PlannedDate == default)
            {
                error = "A planned date is required.";
                return false;
            }

            if (model.RepeatUntil.HasValue && model.RepeatUntil.Value.Date < model.PlannedDate.Date)
            {
                error = "Repeat-until date must be on or after the planned date.";
                return false;
            }

            return true;
        }

        private async Task<string> ResolveUserId(string fallbackUserId)
        {
            var email = User.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var user = await _userRepository.GetByEmail(email);
                if (user != null) return user.Id;
            }

            return string.IsNullOrWhiteSpace(fallbackUserId) ? null : fallbackUserId;
        }
    }
}
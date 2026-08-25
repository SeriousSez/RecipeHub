using AutoMapper;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Entities.Plan;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using RecipeHub.Infrastructure.Interfaces;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public class FoodPlanService : IFoodPlanService
    {
        private readonly IPlanRepository _planRepository;
        private readonly IRecipeRepository _recipeRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public FoodPlanService(IPlanRepository planRepository, IRecipeRepository recipeRepository, IUserRepository userRepository, IMapper mapper)
        {
            _planRepository = planRepository;
            _recipeRepository = recipeRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<ICollection<FoodPlanEntryResponse>> Get(string userId, DateTime start, DateTime end)
        {
            var rangeStart = start.Date;
            var rangeEnd = end.Date;
            if (rangeEnd < rangeStart) return new List<FoodPlanEntryResponse>();

            var entries = await _planRepository.GetEntriesByUserId(userId, rangeStart, rangeEnd);
            return entries
                .SelectMany(entry => ExpandOccurrences(entry, rangeStart, rangeEnd))
                .OrderBy(entry => entry.OccurrenceDate)
                .ThenBy(entry => entry.Position)
                .ThenBy(entry => entry.Recipe?.Title)
                .ToList();
        }

        public async Task<FoodPlanEntryResponse> Create(string userId, FoodPlanEntryViewModel model)
        {
            if (!Guid.TryParse(userId, out var parsedUserId)) return null;

            var user = await _userRepository.GetByUserId(parsedUserId);
            var recipe = await _recipeRepository.Get(model.RecipeId);
            if (user == null || recipe == null) return null;

            var entry = new PlannedRecipe
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                RecipeId = recipe.Id,
                Recipe = recipe,
                PlannedDate = model.PlannedDate.Date,
                MealSlot = NormalizeMealSlot(model.MealSlot),
                Notes = model.Notes?.Trim(),
                RepeatWeekly = model.RepeatWeekly,
                RepeatUntil = model.RepeatUntil?.Date,
                Position = model.Position
            };

            await _planRepository.CreateEntry(entry);
            return ToResponse(entry, entry.PlannedDate);
        }

        public async Task<FoodPlanEntryResponse> Update(string userId, FoodPlanEntryViewModel model)
        {
            if (!model.Id.HasValue) return null;

            var entry = await _planRepository.GetEntry(userId, model.Id.Value);
            var recipe = await _recipeRepository.Get(model.RecipeId);
            if (entry == null || recipe == null) return null;

            entry.RecipeId = recipe.Id;
            entry.Recipe = recipe;
            entry.PlannedDate = model.PlannedDate.Date;
            entry.MealSlot = NormalizeMealSlot(model.MealSlot);
            entry.Notes = model.Notes?.Trim();
            entry.RepeatWeekly = model.RepeatWeekly;
            entry.RepeatUntil = model.RepeatUntil?.Date;
            entry.Position = model.Position;
            entry.LastUpdated = DateTime.Now;

            await _planRepository.UpdateEntry(entry);
            return ToResponse(entry, entry.PlannedDate);
        }

        public async Task<bool> Delete(string userId, Guid id)
        {
            var entry = await _planRepository.GetEntry(userId, id);
            if (entry == null) return false;

            await _planRepository.DeleteEntry(entry);
            return true;
        }

        private IEnumerable<FoodPlanEntryResponse> ExpandOccurrences(PlannedRecipe entry, DateTime rangeStart, DateTime rangeEnd)
        {
            var plannedDate = entry.PlannedDate.Date;
            if (!entry.RepeatWeekly)
            {
                if (plannedDate >= rangeStart && plannedDate <= rangeEnd) yield return ToResponse(entry, plannedDate);
                yield break;
            }

            var repeatEnd = entry.RepeatUntil?.Date ?? rangeEnd;
            var occurrenceDate = plannedDate;
            if (occurrenceDate < rangeStart)
            {
                var daysUntilRange = (rangeStart - occurrenceDate).Days;
                occurrenceDate = occurrenceDate.AddDays((int)Math.Ceiling(daysUntilRange / 7d) * 7);
            }

            while (occurrenceDate <= rangeEnd && occurrenceDate <= repeatEnd)
            {
                yield return ToResponse(entry, occurrenceDate);
                occurrenceDate = occurrenceDate.AddDays(7);
            }
        }

        private FoodPlanEntryResponse ToResponse(PlannedRecipe entry, DateTime occurrenceDate)
        {
            var response = _mapper.Map<FoodPlanEntryResponse>(entry);
            response.OccurrenceDate = occurrenceDate.Date;
            return response;
        }

        private static string NormalizeMealSlot(string mealSlot)
        {
            return string.IsNullOrWhiteSpace(mealSlot) ? "Dinner" : mealSlot.Trim();
        }
    }
}
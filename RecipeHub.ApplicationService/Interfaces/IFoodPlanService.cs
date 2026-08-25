using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Interfaces
{
    public interface IFoodPlanService
    {
        Task<ICollection<FoodPlanEntryResponse>> Get(string userId, DateTime start, DateTime end);
        Task<FoodPlanEntryResponse> Create(string userId, FoodPlanEntryViewModel model);
        Task<FoodPlanEntryResponse> Update(string userId, FoodPlanEntryViewModel model);
        Task<bool> Delete(string userId, Guid id);
    }
}
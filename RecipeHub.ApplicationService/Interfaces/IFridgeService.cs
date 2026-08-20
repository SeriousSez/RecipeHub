using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Interfaces
{
    public interface IFridgeService
    {
        Task<ICollection<FridgeResponse>> Get(Guid userId);
        Task Add(FridgeModel model);
        Task AddHomeFridge(User user);
        Task Retire(Guid fridgeId);
        Task UnRetire(Guid fridgeId);
        Task<ICollection<FridgeGroceryResponse>> GetGroceries(Guid fridgeId);
        Task AddGrocery(FridgeGroceryModel model);
        Task RemoveGrocery(Guid id);
    }
}

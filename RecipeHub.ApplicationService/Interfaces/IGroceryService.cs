using RecipeHub.Domain.Responses;
using System;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Interfaces
{
    public interface IGroceryService
    {
        Task<GroceryListResponse> GetGroceryList(string userId);
        Task Create(Guid userId);
    }
}

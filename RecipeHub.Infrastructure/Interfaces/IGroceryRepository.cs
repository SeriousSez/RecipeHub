using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Grocery;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Interfaces
{
    public interface IGroceryRepository
    {
        Task Create(GroceryList list);
        Task<GroceryList> Get(Guid id);
        Task<GroceryList> GetByUserId(string userId);
        Task<GroceryList> GetByUserName(string listname);
        Task<GroceryList> GetByUser(User list);
        Task<IEnumerable<GroceryList>> GetAll();
        Task Update(GroceryList list);
        Task Delete(GroceryList list);
    }
}

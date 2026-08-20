using RecipeHub.Domain.Entities.Fridge;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Interfaces
{
    public interface IFridgeGroceryRepository : IBaseRepository<FridgeGrocery>
    {
        Task<ICollection<FridgeGrocery>> GetByFridgeId(Guid id);
    }
}

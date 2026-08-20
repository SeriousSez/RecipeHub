using RecipeHub.Domain.Entities.Fridge;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Interfaces
{
    public interface IFridgeRepository : IBaseRepository<Fridge>
    {
        Task<ICollection<Fridge>> GetAllByUserId(Guid id);
    }
}

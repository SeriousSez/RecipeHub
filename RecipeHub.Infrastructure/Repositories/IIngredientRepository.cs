using RecipeHub.Domain.Entities.Recipe;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IIngredientRepository : IBaseRepository<Ingredient>
    {
        Task<bool> Exists(string name);
        Task<Ingredient> GetFull(Guid id);
        Task<Ingredient> GetByName(string name);
        Task<Ingredient> GetByNameFull(string name);
        Task<IEnumerable<Ingredient>> GetAllLite();
        Task<IEnumerable<Ingredient>> GetAllFull();
    }
}
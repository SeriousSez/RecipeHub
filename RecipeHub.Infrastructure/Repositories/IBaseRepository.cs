using RecipeHub.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IBaseRepository<T> where T : BaseEntity
    {
        Task<T> Create(T baseEntity);
        Task<T> Get(Guid id);
        Task<IEnumerable<T>> GetAll();
        Task Update(T baseEntity);
        Task Delete(T baseEntity);
        Task Delete(Guid id);
        Task DeleteRange(List<T> baseEntities);
    }
}
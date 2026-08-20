using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public abstract class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
    {
        protected readonly RecipeHubContext _context;
        protected internal DbSet<T> _dbSet;

        public BaseRepository(RecipeHubContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<T> Create(T baseEntity)
        {
            var entity = await _dbSet.AddAsync(baseEntity);
            await _context.SaveChangesAsync();
            return entity.Entity;
        }

        public async Task<T> Get(Guid id)
        {
            return await _dbSet.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<IEnumerable<T>> GetAll()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task Update(T baseEntity)
        {
            _dbSet.Update(baseEntity);
            await _context.SaveChangesAsync();
        }

        public async Task Delete(T baseEntity)
        {
            _dbSet.Remove(baseEntity);
            await _context.SaveChangesAsync();
        }

        public async Task Delete(Guid id)
        {
            var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<T> baseEntities)
        {
            _dbSet.RemoveRange(baseEntities);
            await _context.SaveChangesAsync();
        }
    }
}

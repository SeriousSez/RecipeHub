using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Grocery;
using RecipeHub.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories.Grocery
{
    public class GroceryRepository : IGroceryRepository
    {
        private readonly RecipeHubContext _context;

        public GroceryRepository(RecipeHubContext context)
        {
            _context = context;
        }

        public async Task Create(GroceryList list)
        {
            await _context.GroceryLists.AddAsync(list);
            await _context.SaveChangesAsync();
        }

        public async Task<GroceryList> Get(Guid id)
        {
            var list = await _context.GroceryLists.Include(u => u.User).FirstOrDefaultAsync(u => u.Id == id);
            return list;
        }

        public async Task<GroceryList> GetByUserId(string userId)
        {
            var list = await _context.GroceryLists.Include(u => u.User).FirstOrDefaultAsync(u => u.User.Id == userId);
            return list;
        }

        public async Task<GroceryList> GetByUserName(string listname)
        {
            var list = await _context.GroceryLists.Include(u => u.User).FirstOrDefaultAsync(u => u.User.UserName == listname);
            return list;
        }

        public async Task<GroceryList> GetByUser(User list)
        {
            var groceryList = await _context.GroceryLists.Include(u => u.User).FirstOrDefaultAsync(u => u.User == list);
            return groceryList;
        }

        public async Task<IEnumerable<GroceryList>> GetAll()
        {
            var lists = await _context.GroceryLists.Include(u => u.User).ToListAsync();
            return lists;
        }

        public async Task Update(GroceryList list)
        {
            _context.GroceryLists.Update(list);
            await _context.SaveChangesAsync();
        }

        public async Task Delete(GroceryList list)
        {
            _context.GroceryLists.Remove(list);
            await _context.SaveChangesAsync();
        }
    }
}

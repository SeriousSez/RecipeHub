using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly RecipeHubContext _context;

        public UserRepository(RecipeHubContext context)
        {
            _context = context;
        }

        public async Task Create(User user)
        {
            await _context.UserSeekers.AddAsync(new UserSeeker { IdentityId = user.Id });
            await _context.SaveChangesAsync();
        }

        public async Task<User> Get(Guid id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id.ToString());
            return user;
        }

        public async Task<User> GetByUserId(Guid id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id.ToString());
            return user;
        }

        public async Task<User> GetByUserName(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            return user;
        }

        public async Task<User> GetByEmail(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user;
        }

        public async Task<UserSeeker> GetByUser(User user)
        {
            var userSeeker = await _context.UserSeekers.Include(u => u.Identity).FirstOrDefaultAsync(u => u.Identity == user);
            return userSeeker;
        }

        public async Task<IEnumerable<User>> GetAll()
        {
            var users = await _context.Users.ToListAsync();
            return users;
        }

        public async Task Delete(User user)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        public async Task CreateSettings(UserSettings settings)
        {
            await _context.UserSettings.AddAsync(settings);
            await _context.SaveChangesAsync();
        }

        public async Task Update(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateSettings(UserSettings settings)
        {
            _context.UserSettings.Update(settings);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSettings(UserSettings settings)
        {
            _context.UserSettings.Remove(settings);
            await _context.SaveChangesAsync();
        }

        public async Task<UserSettings> GetSettings(User user)
        {
            if (user == null)
                return null;

            var settings = await _context.UserSettings
                .Include(s => s.Identity)
                .FirstOrDefaultAsync(s => s.UserId == user.Id);
            return settings;
        }
    }
}

using RecipeHub.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IUserRepository
    {
        Task Create(User user);
        Task<User> Get(Guid id);
        Task<User> GetByUserId(Guid id);
        Task<User> GetByUserName(string username);
        Task<User> GetByEmail(string email);
        Task<UserSeeker> GetByUser(User user);
        Task<IEnumerable<User>> GetAll();
        Task Update(User user);
        Task Delete(User user);
        Task CreateSettings(UserSettings settings);
        Task UpdateSettings(UserSettings settings);
        Task DeleteSettings(UserSettings settings);
        Task<UserSettings> GetSettings(User user);
    }
}
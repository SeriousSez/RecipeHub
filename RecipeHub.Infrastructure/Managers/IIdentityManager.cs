using RecipeHub.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Managers
{
    public interface IIdentityManager
    {
        Task<bool> RoleExistsAsync(string role);
        List<string> GetAllRoles();
        Task<string> GetUserRole(User user);
        Task<bool> CreateRoleAsync(string role);
        Task<bool> CreateUserAsync(User user, string password);
        Task<bool> AddUserToRoleAsync(string userId, string role);
        Task ClearUserRolesAsync(string userId);
    }
}

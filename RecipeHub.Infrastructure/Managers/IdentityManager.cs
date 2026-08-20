using Microsoft.AspNetCore.Identity;
using RecipeHub.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Managers
{
    public class IdentityManager : IIdentityManager
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IdentityManager(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<bool> RoleExistsAsync(string role)
        {
            return await _roleManager.RoleExistsAsync(role);
        }

        public List<string> GetAllRoles()
        {
            var roles = new List<string>();
            var identityRoles = _roleManager.Roles;

            foreach(var role in identityRoles)
            {
                roles.Add(role.Name);
            }

            return roles;
        }

        public async Task<string> GetUserRole(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            return roles.FirstOrDefault();
        }

        public async Task<bool> CreateRoleAsync(string role)
        {
            var result = await _roleManager.CreateAsync(new IdentityRole(role));
            return result.Succeeded;
        }

        public async Task<bool> CreateUserAsync(User user, string password)
        {
            var result = await _userManager.CreateAsync(user, password);
            return result.Succeeded;
        }


        public async Task<bool> AddUserToRoleAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var userRoles = await _userManager.GetRolesAsync(user);
            if(userRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, userRoles);

            var result = await _userManager.AddToRoleAsync(user, role);
            return result.Succeeded;
        }


        public async Task ClearUserRolesAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            foreach (var role in _roleManager.Roles)
            {
                await _userManager.RemoveFromRoleAsync(user, role.Name);
            }
        }
    }
}

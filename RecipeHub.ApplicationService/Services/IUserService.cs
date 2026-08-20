using Microsoft.AspNetCore.Identity;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public interface IUserService
    {
        Task<IdentityResult> Create(RegistrationViewModel model);
        Task<UserResponse> Update(UserUpdateViewModel model);
        Task<IdentityResult> Delete(UserResponse model);
        Task<UserResponse> Get(Guid id);
        Task<User> GetEntity(Guid id);
        Task<UserResponse> GetByUserId(Guid id);
        Task<UserResponse> GetByUserName(string userName);
        Task<UserResponse> GetByEmail(string userName);
        Task<IEnumerable<UserResponse>> GetAll();
        Task<UserSettingsResponse> GetSettings(Guid id);
        Task<UserSettingsResponse> UpdateSettings(UserSettingsUpdateViewModel model);
        Task<(int Created, int Existing)> BackfillMissingSettings();
        List<string> GetRoles();
        Task AddRoleToUser(UserResponse user);
    }
}
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using RecipeHub.Infrastructure.Managers;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace RecipeHub.ApplicationService.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IMapper _mapper;
        private readonly UserManager<User> _userManager;
        private readonly IUserRepository _userRepository;
        private readonly IIdentityManager _identityManager;
        private readonly IFavoriteRepository _favoriteRepository;
        private readonly IRecipeRepository _recipeRepository;
        private readonly IIngredientRepository _ingredientRepository;
        private readonly IFridgeService _fridgeService;

        public UserService(
            ILogger<UserService> logger,
            IMapper mapper,
            UserManager<User> userManager,
            IUserRepository userRepository,
            IIdentityManager identityManager,
            IFavoriteRepository favoriteRepository,
            IRecipeRepository recipeRepository,
            IIngredientRepository ingredientRepository,
            IFridgeService fridgeService)
        {
            _logger = logger;
            _mapper = mapper;
            _userManager = userManager;
            _userRepository = userRepository;
            _identityManager = identityManager;
            _favoriteRepository = favoriteRepository;
            _recipeRepository = recipeRepository;
            _ingredientRepository = ingredientRepository;
            _fridgeService = fridgeService;
        }

        public async Task<IdentityResult> Create(RegistrationViewModel model)
        {
            var userIdentity = _mapper.Map<User>(model);
            var result = await _userManager.CreateAsync(userIdentity, model.Password);
            if (result.Succeeded == false)
                return result;

            _logger.LogTrace("Created UserIdentity. Result: {@Result}", result);

            if (string.IsNullOrEmpty(model.Role) == false)
            {
                if (await _identityManager.RoleExistsAsync(model.Role) == false)
                    await _identityManager.CreateRoleAsync(model.Role);

                await _identityManager.AddUserToRoleAsync(userIdentity.Id, model.Role);
                _logger.LogTrace("Added User: {Username} to Role: {Role}. Result: {@Result}", model.Username, model.Role, result);
            }

            //await _userRepository.Create(userIdentity);
            //await _userManager.CreateAsync(userIdentity, model.Password);
            _logger.LogTrace("User created! User: {@User}", userIdentity);

            await EnsureSettingsExists(userIdentity);

            await _favoriteRepository.Create(new Favorites { UserId = userIdentity.Id });

            return result;
        }

        public async Task<UserResponse> Update(UserUpdateViewModel model)
        {
            var user = await _userRepository.GetByUserName(model.OldUsername);
            if (user == null)
                return null;

            user.UserName = model.Username;
            user.Firstname = model.FirstName;
            user.Lastname = model.LastName;
            user.Email = model.Email;

            if (string.IsNullOrEmpty(model.Role) == false)
            {
                if (await _identityManager.RoleExistsAsync(model.Role) == false)
                    await _identityManager.CreateRoleAsync(model.Role);

                await _identityManager.AddUserToRoleAsync(user.Id, model.Role);
                _logger.LogTrace("Added User: {Username} to Role: {Role}. User: {@User}", model.Username, model.Role, user);
            }

            await _userRepository.Update(user);
            await _userManager.UpdateAsync(user);
            _logger.LogTrace("User created! User: {@User}", user);

            return _mapper.Map<UserResponse>(user);
        }

        public async Task<IdentityResult> Delete(UserResponse model)
        {
            var userIdentity = await _userRepository.GetByUserId(model.Id);

            await DeleteUserForeignEntities(userIdentity);
            var result = await _userManager.DeleteAsync(userIdentity);
            if (result.Succeeded == false)
                return result;

            _logger.LogTrace("Deleted UserIdentity. Result: {@Result}", result);

            _logger.LogTrace("User Deleted! User: {@User}", userIdentity);

            return result;
        }

        public async Task<UserResponse> Get(Guid id)
        {
            var user = await _userRepository.Get(id);
            var userResponse = _mapper.Map<UserResponse>(user);
            userResponse.Role = await _identityManager.GetUserRole(user);

            return userResponse;
        }

        public async Task<User> GetEntity(Guid id)
        {
            var user = await _userRepository.Get(id);
            if (user == null)
                return null;

            return user;
        }

        public async Task<UserResponse> GetByUserId(Guid id)
        {
            var user = await _userRepository.GetByUserId(id);
            var userResponse = _mapper.Map<UserResponse>(user);
            userResponse.Role = await _identityManager.GetUserRole(user);

            return userResponse;
        }

        public async Task<UserResponse> GetByUserName(string userName)
        {
            var user = await _userRepository.GetByUserName(userName);
            if (user == null)
                return null;

            var userResponse = _mapper.Map<UserResponse>(user);
            userResponse.Role = await _identityManager.GetUserRole(user);

            return userResponse;
        }

        public async Task<UserResponse> GetByEmail(string email)
        {
            var user = await _userRepository.GetByEmail(email);
            if (user == null)
                return null;

            var userResponse = _mapper.Map<UserResponse>(user);
            userResponse.Role = await _identityManager.GetUserRole(user);

            return userResponse;
        }

        public async Task<IEnumerable<UserResponse>> GetAll()
        {
            var users = await _userRepository.GetAll();

            var userList = new List<UserResponse>();
            foreach (var user in users)
            {
                var userResponse = _mapper.Map<UserResponse>(user);
                userResponse.Role = await _identityManager.GetUserRole(user);
                userList.Add(userResponse);
            }

            return userList;
        }

        public List<string> GetRoles()
        {
            return _identityManager.GetAllRoles();
        }

        public async Task AddRoleToUser(UserResponse user)
        {
            if (await _identityManager.RoleExistsAsync(user.Role) == false)
                await _identityManager.CreateRoleAsync(user.Role);

            var userIdentity = await _userRepository.GetByUserName(user.UserName);

            await _identityManager.AddUserToRoleAsync(userIdentity.Id, user.Role);
            _logger.LogTrace($"Added User: {user.UserName} to Role: {user.Role}");
        }

        private async Task CreateSettings(User user)
        {
            if (user == null)
                return;

            var existingSettings = await _userRepository.GetSettings(user);
            if (existingSettings != null)
                return;

            var settings = CreateDefaultSettings(user);

            await _userRepository.CreateSettings(settings);
        }

        public async Task<(int Created, int Existing)> BackfillMissingSettings()
        {
            var users = await _userRepository.GetAll();
            var created = 0;
            var existing = 0;

            foreach (var user in users)
            {
                if (user == null)
                    continue;

                var settings = await _userRepository.GetSettings(user);
                if (settings != null)
                {
                    existing++;
                    continue;
                }

                await _userRepository.CreateSettings(CreateDefaultSettings(user));
                created++;
            }

            return (created, existing);
        }

        public async Task<UserSettingsResponse> UpdateSettings(UserSettingsUpdateViewModel model)
        {
            try
            {
                var user = await _userRepository.GetByUserId(model.UserId);
                if (user == null)
                    return null;

                var settings = await EnsureSettingsExists(user);
                if (settings == null)
                    return null;

                settings.PreferredLanguage = model.PreferredLanguage ?? settings.PreferredLanguage ?? "English";
                settings.Theme = string.IsNullOrWhiteSpace(model.Theme) ? settings.Theme ?? "Light" : model.Theme;
                settings.RecipesTheme = string.IsNullOrWhiteSpace(model.RecipesTheme) ? settings.RecipesTheme ?? "Pretty" : model.RecipesTheme;
                settings.MyRecipesTheme = string.IsNullOrWhiteSpace(model.MyRecipesTheme) ? settings.MyRecipesTheme ?? settings.RecipesTheme ?? "Pretty" : model.MyRecipesTheme;

                await _userRepository.UpdateSettings(settings);
                _logger.LogTrace("User created! User: {@User}", user);

                var response = _mapper.Map<UserSettingsResponse>(settings);
                response.MyRecipesTheme = settings.MyRecipesTheme ?? settings.RecipesTheme ?? "Pretty";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falling back to default user settings because the settings table schema is out of sync for user {UserId}", model?.UserId);
                return new UserSettingsResponse
                {
                    PreferredLanguage = "English",
                    Theme = "Light",
                    RecipesTheme = "Pretty",
                    MyRecipesTheme = "Pretty"
                };
            }
        }

        public async Task<UserSettingsResponse> GetSettings(Guid id)
        {
            try
            {
                var user = await _userRepository.GetByUserId(id);
                if (user == null)
                    return null;

                var settings = await EnsureSettingsExists(user);
                if (settings == null)
                    return null;

                settings.MyRecipesTheme = settings.MyRecipesTheme ?? settings.RecipesTheme ?? "Pretty";
                var settingsResponse = _mapper.Map<UserSettingsResponse>(settings);
                settingsResponse.MyRecipesTheme = settings.MyRecipesTheme ?? "Pretty";

                return settingsResponse;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falling back to default user settings because the settings table schema is out of sync for user {UserId}", id);
                return new UserSettingsResponse
                {
                    PreferredLanguage = "English",
                    Theme = "Light",
                    RecipesTheme = "Pretty",
                    MyRecipesTheme = "Pretty"
                };
            }
        }

        public async Task<List<PantryItemViewModel>> GetPantry(Guid id)
        {
            var user = await _userRepository.GetByUserId(id);
            if (user == null)
                return null;

            var settings = await EnsureSettingsExists(user);
            if (string.IsNullOrWhiteSpace(settings.PantryItems))
                return new List<PantryItemViewModel>();

            try
            {
                return JsonSerializer.Deserialize<List<PantryItemViewModel>>(settings.PantryItems) ?? new List<PantryItemViewModel>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ignoring malformed pantry data for user {UserId}", id);
                return new List<PantryItemViewModel>();
            }
        }

        public async Task<List<PantryItemViewModel>> UpdatePantry(PantryUpdateViewModel model)
        {
            var user = await _userRepository.GetByUserId(model.UserId);
            if (user == null)
                return null;

            var settings = await EnsureSettingsExists(user);
            var items = model.Items ?? new List<PantryItemViewModel>();
            settings.PantryItems = JsonSerializer.Serialize(items);
            await _userRepository.UpdateSettings(settings);
            return items;
        }

        private async Task DeleteUserForeignEntities(User user)
        {
            var favorites = await _favoriteRepository.GetByUserForDeletion(user);
            if (favorites != null)
            {
                if (favorites.Recipes.Count > 0)
                {
                    await _recipeRepository.DeleteRange(favorites.Recipes);
                }

                if (favorites.Ingredients.Count > 0)
                    await _ingredientRepository.DeleteRange(favorites.Ingredients);

                await _favoriteRepository.Delete(favorites);
            }

            var settings = await _userRepository.GetSettings(user);
            if (settings != null)
                await _userRepository.DeleteSettings(settings);
        }

        private async Task<UserSettings> EnsureSettingsExists(User user)
        {
            if (user == null)
                return null;

            var settings = await _userRepository.GetSettings(user);
            if (settings != null)
                return settings;

            settings = CreateDefaultSettings(user);
            await _userRepository.CreateSettings(settings);
            return settings;
        }

        private static UserSettings CreateDefaultSettings(User user)
        {
            return new UserSettings
            {
                Theme = "Light",
                RecipesTheme = "Pretty",
                MyRecipesTheme = "Pretty",
                PantryItems = "[]",
                UserId = user.Id,
                Identity = user
            };
        }
    }
}

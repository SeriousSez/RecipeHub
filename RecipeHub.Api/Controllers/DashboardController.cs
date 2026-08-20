using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer", Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IUserService _userService;

        public DashboardController(ILogger<AccountController> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        [HttpPost("createuser")]
        public async Task<IActionResult> CreateUser([FromBody] RegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var identityResult = await _userService.Create(model);
            if (identityResult.Succeeded == false)
                return new BadRequestObjectResult((identityResult, ModelState));

            return new OkResult();
        }

        [HttpPost("deleteusers")]
        public async Task<IActionResult> DeleteUsers([FromBody] List<UserResponse> users)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await DeleteUsersAsync(users);

            _logger.LogTrace("Users have been deleted! Users: {@Users}", users);

            var cleanedUsers = await _userService.GetAll();

            return new OkObjectResult(cleanedUsers);
        }

        [HttpGet("getusers")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userService.GetAll();
            if (users == null)
            {
                _logger.LogError("Failed to fetch users!");
                return new NotFoundObjectResult("Failed to fetch users!");
            }

            _logger.LogTrace("Users fetched! Users: {@Users}", users);
            return new OkObjectResult(users);
        }

        [HttpGet("getroles")]
        public IActionResult GetRoles()
        {
            var roles = _userService.GetRoles();
            if (roles == null)
            {
                _logger.LogError("Failed to fetch roles!");
                return new NotFoundObjectResult("Failed to fetch roles!");
            }

            _logger.LogTrace("Roles fetched! Roles: {@Roles}", roles);
            return new OkObjectResult(roles);
        }

        [HttpPost("addrole")]
        public async Task<IActionResult> PostAddRoleToUser([FromBody] UserResponse user)
        {
            await _userService.AddRoleToUser(user);
            return new OkResult();
        }

        private async Task DeleteUsersAsync(List<UserResponse> users)
        {
            foreach (var user in users)
            {
                var result = await _userService.Delete(user);
                if (result.Succeeded == false)
                    _logger.LogError("Failed to delete user {UserId}. Errors: {@Errors}", user.Id, result.Errors);
            }
        }
    }
}

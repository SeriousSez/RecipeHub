using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _environment;

        public AccountController(ILogger<AccountController> logger, IUserService userService, IAuthService authService, IWebHostEnvironment environment)
        {
            _logger = logger;
            _userService = userService;
            _authService = authService;
            _environment = environment;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get(string userName)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.GetByUserName(userName);
            if (user == null)
                return new NotFoundResult();

            return new OkObjectResult(user);
        }

        [AllowAnonymous]
        [HttpGet("public/{username}")]
        public async Task<IActionResult> GetPublicProfile(string username)
        {
            var profile = await _userService.GetPublicProfile(username);
            return profile == null ? NotFound() : Ok(profile);
        }

        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpPut("public-profile")]
        public async Task<IActionResult> UpdatePublicProfile([FromBody] PublicProfileUpdateViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentUser = await _userService.GetByEmail(User.Identity?.Name);
            if (currentUser == null || currentUser.Id != model.UserId)
                return Forbid();

            var profile = await _userService.UpdatePublicProfile(model);
            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] RegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(model.Role))
                model.Role = "User";

            try
            {
                var identityResult = await _userService.Create(model);
                if (identityResult.Succeeded == false)
                {
                    return BadRequest(new
                    {
                        Code = "registration_invalid",
                        Message = "The account could not be created.",
                        Errors = identityResult.Errors.Select(error => error.Description).ToArray()
                    });
                }

                var confirmationResult = await _authService.SendEmailConfirmation(model.Email);
                if (!confirmationResult.Succeeded)
                {
                    return StatusCode(503, new
                    {
                        Code = "confirmation_email_unavailable",
                        Message = "Your account was created, but we could not send the confirmation email.",
                        Errors = confirmationResult.Errors.Select(error => error.Description).ToArray()
                    });
                }

                return new OkResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Registration failed for {Email}.", model.Email);

                return StatusCode(500, new
                {
                    Code = "registration_failed",
                    Message = "Registration could not be completed.",
                    Detail = string.Equals(_environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase)
                        ? exception.GetBaseException().Message
                        : null
                });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UserUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.Update(model);
            if (user == null)
                return BadRequest("Failed to upgrade user!");

            _logger.LogTrace("User has been updated! User: {@User}", user);

            return new OkObjectResult(user);
        }

        [HttpGet("getsettings")]
        public async Task<IActionResult> GetSettings(Guid userId)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var settings = await _userService.GetSettings(userId);
            if (settings == null)
                return new NotFoundResult();

            return new OkObjectResult(settings);
        }

        [HttpPost("updatesettings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UserSettingsUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var settings = await _userService.UpdateSettings(model);
            if (settings == null)
                return BadRequest("Failed to upgrade user settings!");

            _logger.LogTrace("User settings have been updated! Settings: {@Settings}", settings);

            return new OkResult();
        }

        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpGet("getpantry")]
        public async Task<IActionResult> GetPantry(Guid userId)
        {
            if (!await CanAccessUser(userId))
                return Forbid();

            var items = await _userService.GetPantry(userId);
            return items == null ? NotFound() : Ok(items);
        }

        [Authorize(AuthenticationSchemes = "Bearer")]
        [HttpPost("updatepantry")]
        public async Task<IActionResult> UpdatePantry([FromBody] PantryUpdateViewModel model)
        {
            if (model == null || !await CanAccessUser(model.UserId))
                return Forbid();

            var items = await _userService.UpdatePantry(model);
            return items == null ? NotFound() : Ok(items);
        }

        private async Task<bool> CanAccessUser(Guid userId)
        {
            var email = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var currentUser = await _userService.GetByEmail(email);
            return currentUser != null && currentUser.Id == userId;
        }

        [Authorize(AuthenticationSchemes = "Bearer", Roles = "Admin")]
        [HttpPost("backfillsettings")]
        public async Task<IActionResult> BackfillSettings()
        {
            var (created, existing) = await _userService.BackfillMissingSettings();

            _logger.LogInformation("User settings backfill completed. Created: {Created}, Existing: {Existing}", created, existing);

            return new OkObjectResult(new
            {
                Created = created,
                Existing = existing
            });
        }
    }
}

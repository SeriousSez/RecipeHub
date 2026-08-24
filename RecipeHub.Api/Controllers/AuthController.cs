using Microsoft.AspNetCore.Mvc;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [Route("[controller]")]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] CredentialsViewModel credentials)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.Login(credentials);
            if (response == null)
                return BadRequest(("Login Failure", "Invalid username or password.", ModelState));

            if (!response.EmailConfirmed)
                return StatusCode(403, new
                {
                    Code = "email_confirmation_required",
                    Email = response.Email,
                    Message = "Please confirm your email address before logging in."
                });

            //var json = JsonConvert.SerializeObject(response, _serializerSettings);
            return new OkObjectResult(response);
        }

        [HttpPost("forgotpassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.GeneratePasswordResetToken(model);
            if (!response.Success)
                return StatusCode(503, response);

            return new OkObjectResult(response);
        }

        [HttpPost("resetpassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.ResetPassword(model);
            if (result.Succeeded == false)
                return BadRequest(result.Errors);

            return new OkResult();
        }

        [HttpGet("confirmemail")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
                return BadRequest("The confirmation link is incomplete.");

            var result = await _authService.ConfirmEmail(userId, token);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok();
        }

        [HttpPost("resendconfirmation")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model?.Email))
                return BadRequest(ModelState);

            var result = await _authService.SendEmailConfirmation(model.Email);
            if (!result.Succeeded)
                return StatusCode(503, new
                {
                    Code = "confirmation_email_unavailable",
                    Message = "We could not send the confirmation email. Please try again later."
                });

            return Ok(new
            {
                Message = "A new confirmation email has been sent."
            });
        }
    }
}

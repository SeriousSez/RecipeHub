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
    }
}

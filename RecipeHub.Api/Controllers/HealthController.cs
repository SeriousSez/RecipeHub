using Microsoft.AspNetCore.Mvc;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return new OkResult();
        }
    }
}

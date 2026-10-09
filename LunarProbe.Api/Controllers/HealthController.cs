
using Microsoft.AspNetCore.Mvc;

namespace LunarProbe.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            application = "Lunar Probe Intelligence",
            timestampUtc = DateTime.UtcNow
        });
    }
}

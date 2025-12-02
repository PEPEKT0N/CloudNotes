using Microsoft.AspNetCore.Mvc;

namespace CloudNotes.Api.Controllers;

/// <summary>
/// Health check controller для проверки работоспособности API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Проверка работоспособности API
    /// </summary>
    /// <returns>Статус API</returns>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }
}


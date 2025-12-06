using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudNotes.Api.Controllers;

/// <summary>
/// Health check controller для проверки работоспособности API.
/// Рекомендуется использовать встроенный endpoint /health.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Проверка работоспособности API с детальной информацией о зависимостях.
    /// </summary>
    /// <returns>Статус API и состояние зависимостей.</returns>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync();

        var status = healthReport.Status == HealthStatus.Healthy ? "healthy" : 
                     healthReport.Status == HealthStatus.Degraded ? "degraded" : "unhealthy";

        return Ok(new
        {
            status,
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            checks = healthReport.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLower(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description
            })
        });
    }
}


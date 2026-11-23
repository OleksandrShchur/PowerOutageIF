using Microsoft.AspNetCore.Mvc;
using PowerOutageIF.Services;

namespace PowerOutageIF.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly IBroadcastQueue _broadcastQueue;

    public HealthController(IBroadcastQueue broadcastQueue)
    {
        _broadcastQueue = broadcastQueue;
    }

    [HttpGet("/")]
    public IActionResult GetRoot()
    {
        return Content("⚡ PowerOutageIF Telegram Bot Service is running.", "text/plain");
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            serverTimeUtc = DateTime.UtcNow,
            kyivTime = ScheduleTrackerService.GetKyivNow().ToString("yyyy-MM-dd HH:mm:ss"),
            pendingBroadcasts = _broadcastQueue.PendingCount
        });
    }
}

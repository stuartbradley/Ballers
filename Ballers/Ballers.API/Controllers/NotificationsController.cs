using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ballers.API.Models;

namespace Ballers.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(INotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _notifications = notifications;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();
        return Ok(await _notifications.GetForUserAsync(userId));
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();
        await _notifications.MarkReadAsync(id, userId);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();
        await _notifications.MarkAllReadAsync(userId);
        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
        => Ok(await _notifications.GetSettingsAsync());

    [Authorize(Roles = "Admin")]
    [HttpPut("settings/{type}")]
    public async Task<IActionResult> UpdateSetting(NotificationType type, [FromBody] bool isEnabled)
    {
        await _notifications.UpdateSettingAsync(type, isEnabled);
        return Ok();
    }
}

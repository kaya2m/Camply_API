using Camply.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers.Chat
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PresenceController : ControllerBase
    {
        private readonly UserPresenceTracker _presenceTracker;
        private readonly ILogger<PresenceController> _logger;

        public PresenceController(
            UserPresenceTracker presenceTracker,
            ILogger<PresenceController> logger)
        {
            _presenceTracker = presenceTracker;
            _logger = logger;
        }

        [HttpGet("online/{userId}")]
        public async Task<ActionResult<bool>> IsUserOnline(string userId)
        {
            try
            {
                var isOnline = await _presenceTracker.IsUserOnline(userId);
                return Ok(isOnline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking online status for user {userId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("lastseen/{userId}")]
        public async Task<ActionResult<DateTime?>> GetUserLastSeen(string userId)
        {
            try
            {
                var lastSeen = await _presenceTracker.GetLastSeenAt(userId);
                return Ok(lastSeen);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting last seen time for user {userId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("online")]
        public async Task<ActionResult<IEnumerable<string>>> GetOnlineUsers()
        {
            try
            {
                var onlineUsers = await _presenceTracker.GetOnlineUsers();
                return Ok(onlineUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online users");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}

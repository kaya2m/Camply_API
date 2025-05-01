using Camply.Application.Users.DTOs;
using Camply.Application.Users.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserService userService,
            ICurrentUserService currentUserService,
            ILogger<UserController> logger)
        {
            _userService = userService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get the profile of a user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User profile information</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserProfileResponse>> GetUserById(Guid id)
        {
            try
            {
                var profile = await _userService.GetUserProfileAsync(id, _currentUserService.UserId);
                return Ok(profile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the user profile" });
            }
        }

        /// <summary>
        /// Get the profile of a user by username
        /// </summary>
        /// <param name="username">Username</param>
        /// <returns>User profile information</returns>
        [HttpGet("by-username/{username}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserProfileResponse>> GetUserByUsername(string username)
        {
            try
            {
                var profile = await _userService.GetUserProfileByUsernameAsync(username, _currentUserService.UserId);
                return Ok(profile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user with username '{username}'");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the user profile" });
            }
        }

        /// <summary>
        /// Get the profile of the currently authenticated user
        /// </summary>
        /// <returns>Current user's profile information</returns>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserProfileResponse>> GetCurrentUser()
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var profile = await _userService.GetUserProfileAsync(userId.Value, userId);
                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user profile");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the user profile" });
            }
        }

        /// <summary>
        /// Update the profile of the currently authenticated user
        /// </summary>
        /// <param name="request">Profile update information</param>
        /// <returns>Updated user profile</returns>
        [HttpPut("me")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var updatedProfile = await _userService.UpdateProfileAsync(userId.Value, request);
                return Ok(updatedProfile);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the profile" });
            }
        }

        /// <summary>
        /// Change the password of the currently authenticated user
        /// </summary>
        /// <param name="request">Password change information</param>
        /// <returns>Success result</returns>
        [HttpPut("me/change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var result = await _userService.ChangePasswordAsync(userId.Value, request);
                if (result)
                {
                    return Ok(new { message = "Password changed successfully" });
                }

                return BadRequest(new { message = "Failed to change password" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while changing the password" });
            }
        }

        /// <summary>
        /// Follow another user
        /// </summary>
        /// <param name="id">ID of the user to follow</param>
        /// <returns>Success result</returns>
        [HttpPost("{id}/follow")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FollowUser(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                // Cannot follow yourself
                if (userId.Value == id)
                {
                    return BadRequest(new { message = "You cannot follow yourself" });
                }

                var result = await _userService.FollowUserAsync(userId.Value, id);
                return Ok(new { message = "User followed successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error following user with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while following the user" });
            }
        }

        /// <summary>
        /// Unfollow another user
        /// </summary>
        /// <param name="id">ID of the user to unfollow</param>
        /// <returns>Success result</returns>
        [HttpDelete("{id}/follow")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UnfollowUser(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var result = await _userService.UnfollowUserAsync(userId.Value, id);
                return Ok(new { message = "User unfollowed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unfollowing user with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while unfollowing the user" });
            }
        }

        /// <summary>
        /// Get the followers of a user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20)</param>
        /// <returns>Paged list of followers</returns>
        [HttpGet("{id}/followers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedList<UserSummaryResponse>>> GetFollowers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                try
                {
                    await _userService.GetUserProfileAsync(id);
                }
                catch (KeyNotFoundException ex)
                {
                    return NotFound(new { message = ex.Message });
                }

                var followers = await _userService.GetFollowersAsync(id, page, pageSize, _currentUserService.UserId);
                return Ok(followers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving followers for user with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving followers" });
            }
        }

        /// <summary>
        /// Get the users followed by a user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20)</param>
        /// <returns>Paged list of followed users</returns>
        [HttpGet("{id}/following")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedList<UserSummaryResponse>>> GetFollowing(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                // Validate user exists
                try
                {
                    await _userService.GetUserProfileAsync(id);
                }
                catch (KeyNotFoundException ex)
                {
                    return NotFound(new { message = ex.Message });
                }

                var following = await _userService.GetFollowingAsync(id, page, pageSize, _currentUserService.UserId);
                return Ok(following);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving followed users for user with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving followed users" });
            }
        }
    }
}

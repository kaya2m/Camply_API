using Camply.Application.Common.Interfaces;
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
        /// Get the profile of a user by username index
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="limit"> limit</param>
        /// <returns>Users information</returns>
        [HttpGet("search-user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserProfileResponse>> GetSearchUsers([FromQuery] string username, [FromQuery] int limit = 10)
        {
            try
            {
                var profile = await _userService.SearchUsersByUsernameAsync(username, limit);
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

        /// <summary>
        /// İstemci türünü belirlemek için yardımcı metod
        /// </summary>

        /// <summary>
        /// Şifre sıfırlama isteği gönderir
        /// </summary>
        /// <param name="request">Şifre sıfırlama isteği bilgileri</param>
        /// <returns>İşlem sonucu</returns>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                {
                    return BadRequest(new { message = "Email adresi gereklidir" });
                }


                var result = await _userService.ForgotPassword(request.Email);

                return Ok(new
                {
                    success = true,
                    message = "Şifre sıfırlama talimatları, kayıtlı email adresinize gönderildi."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama isteği sırasında hata oluştu");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Şifre sıfırlama isteği işlenirken bir hata oluştu" });
            }
        }
        /// <summary>
        /// Şifre sıfırlama kodunu doğrular
        /// </summary>
        [HttpPost("verify-reset-code")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
                {
                    return BadRequest(new { message = "Email ve kod alanları gereklidir" });
                }

                var isVerified = await _userService.VerifyResetCode(request.Email, request.Code);

                if (!isVerified)
                {
                    return BadRequest(new { message = "Geçersiz veya süresi dolmuş kod" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Kod başarıyla doğrulandı"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kod doğrulama sırasında hata oluştu");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Kod doğrulama sırasında bir hata oluştu" });
            }
        }

        /// <summary>
        /// Token ile şifre sıfırlama işlemini gerçekleştirir
        /// </summary>
        /// <param name="request">Şifre sıfırlama bilgileri</param>
        /// <returns>İşlem sonucu</returns>
        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                {
                    return BadRequest(new { message = "Email gereklidir" });
                }

                if (string.IsNullOrEmpty(request.NewPassword))
                {
                    return BadRequest(new { message = "Yeni şifre gereklidir" });
                }

                var result = await _userService.ResetPassword(request.Email, request.NewPassword);

                return Ok(new
                {
                    success = true,
                    message = "Şifreniz başarıyla sıfırlandı. Yeni şifrenizle giriş yapabilirsiniz."
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama işlemi sırasında hata oluştu");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Şifre sıfırlama işlemi sırasında bir hata oluştu" });
            }
        }

        /// <summary>
        /// Send verification email to the current user
        /// </summary>
        [HttpPost("send-verification-email")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendVerificationEmail()
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var result = await _userService.SendEmailVerificationCodeAsync(userId.Value);
                return Ok(new
                {
                    success = true,
                    message = "Doğrulama kodu başarıyla gönderildi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Doğrulama kodu gönderilirken bir hata oluştu" });
            }
        }

        /// <summary>
        /// Verify a user's email address
        /// </summary>
        [HttpPost("verify-email")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
                {
                    return BadRequest(new { message = "E-posta ve doğrulama kodu gereklidir" });
                }

                var isVerified = await _userService.VerifyEmailAsync(request.Email, request.Code);

                if (!isVerified)
                {
                    return BadRequest(new { message = "Geçersiz veya süresi dolmuş doğrulama kodu" });
                }

                return Ok(new
                {
                    success = true,
                    message = "E-posta başarıyla doğrulandı"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "E-posta doğrulanırken bir hata oluştu" });
            }
        }

        /// <summary>
        /// Resend verification email to a user
        /// </summary>
        [HttpPost("resend-verification-email")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                {
                    return BadRequest(new { message = "E-posta adresi gereklidir" });
                }

                await _userService.ResendEmailVerificationCodeAsync(request.Email);

                return Ok(new
                {
                    success = true,
                    message = "E-posta adresiniz kayıtlı ve doğrulanmamışsa, doğrulama kodu gönderilmiştir"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification email");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Doğrulama kodu yeniden gönderilirken bir hata oluştu" });
            }
        }
    }
}

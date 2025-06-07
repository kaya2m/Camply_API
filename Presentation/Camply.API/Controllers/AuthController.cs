using Camply.Application.Auth.DTOs.Request;
using Camply.Application.Auth.DTOs.Response;
using Camply.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers
{  [ApiController]
   [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        
        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }
        
        /// <summary>
        /// Registers a new user
        /// </summary>
        /// <param name="request">User registration information</param>
        /// <returns>Authentication result with JWT token</returns>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                
                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in user registration");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during registration" });
            }
        }
        
        /// <summary>
        /// Authenticates a user
        /// </summary>
        /// <param name="request">User credentials</param>
        /// <returns>Authentication result with JWT token</returns>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                
                if (!result.Success)
                {
                    return Unauthorized(new { message = result.Message });
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in user login");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during login" });
            }
        }
        /// <summary>
        /// Authenticates a user via social login
        /// </summary>
        /// <param name="provider">Social provider name (google, facebook, twitter)</param>
        /// <param name="request">Social login token</param>
        /// <returns>Authentication result with JWT token</returns>
        [HttpPost("social/{provider}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> SocialLogin(string provider, [FromBody] SocialLoginRequest request)
        {
            try
            {
                request.Provider = provider;    
                
                var result = await _authService.SocialLoginAsync(request);
                
                if (!result.Success)
                {
                    return Unauthorized(new { message = result.Message });
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in social login");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during social login" });
            }
        }
        
        /// <summary>
        /// Refreshes an authentication token
        /// </summary>
        /// <param name="request">Refresh token</param>
        /// <returns>New authentication result with JWT token</returns>
        [HttpPost("refresh-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);
                
                if (!result.Success)
                {
                    return Unauthorized(new { message = result.Message });
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while refreshing token" });
            }
        }
        
        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        /// <param name="request">Refresh token to revoke</param>
        /// <returns>Success result</returns>
        [HttpPost("revoke-token")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                var result = await _authService.RevokeTokenAsync(request.RefreshToken);
                
                if (!result)
                {
                    return BadRequest(new { message = "Invalid token" });
                }
                
                return Ok(new { message = "Token revoked" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while revoking token" });
            }
        }
        
        /// <summary>
        /// Validates a JWT token
        /// </summary>
        /// <returns>Success result</returns>
        [HttpGet("validate-token")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult ValidateToken()
        {
            // If we got here, the token is valid (Authorize attribute)
            return Ok(new { isValid = true });
        }
    }
}

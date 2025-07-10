using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;
using Camply.Application.Users.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Camply.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserRecommendationController : ControllerBase
    {
        private readonly IUserRecommendationService _userRecommendationService;
        private readonly ILogger<UserRecommendationController> _logger;

        public UserRecommendationController(
            IUserRecommendationService userRecommendationService,
            ILogger<UserRecommendationController> logger)
        {
            _userRecommendationService = userRecommendationService;
            _logger = logger;
        }

        [HttpGet("suggestions")]
        public async Task<ActionResult<PagedResponse<UserRecommendationResponse>>> GetUserRecommendations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string algorithm = "smart")
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var request = new UserRecommendationRequest
                {
                    UserId = currentUserId,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 50), // Max 50 per page
                    Algorithm = algorithm,
                    IncludeMutualFollowers = true,
                    ExcludeAlreadyFollowed = true
                };

                var recommendations = await _userRecommendationService.GetUserRecommendationsAsync(request);
                
                _logger.LogInformation("User recommendations retrieved for user: {UserId}, Algorithm: {Algorithm}, Page: {PageNumber}",
                    currentUserId, algorithm, pageNumber);
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user recommendations");
                return StatusCode(500, new { message = "An error occurred while retrieving recommendations" });
            }
        }

        [HttpGet("popular")]
        public async Task<ActionResult<List<UserRecommendationResponse>>> GetPopularUsers(
            [FromQuery] int count = 10)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var recommendations = await _userRecommendationService.GetPopularUsersAsync(currentUserId, Math.Min(count, 50));
                
                _logger.LogInformation("Popular users retrieved for user: {UserId}, Count: {Count}",
                    currentUserId, count);
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving popular users");
                return StatusCode(500, new { message = "An error occurred while retrieving popular users" });
            }
        }

        [HttpGet("mutual-followers")]
        public async Task<ActionResult<List<UserRecommendationResponse>>> GetMutualFollowersRecommendations(
            [FromQuery] int count = 10)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var recommendations = await _userRecommendationService.GetMutualFollowersRecommendationsAsync(currentUserId, Math.Min(count, 50));
                
                _logger.LogInformation("Mutual followers recommendations retrieved for user: {UserId}, Count: {Count}",
                    currentUserId, count);
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving mutual followers recommendations");
                return StatusCode(500, new { message = "An error occurred while retrieving mutual followers recommendations" });
            }
        }

        [HttpGet("recent-active")]
        public async Task<ActionResult<List<UserRecommendationResponse>>> GetRecentActiveUsers(
            [FromQuery] int count = 10)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var recommendations = await _userRecommendationService.GetRecentActiveUsersAsync(currentUserId, Math.Min(count, 50));
                
                _logger.LogInformation("Recent active users retrieved for user: {UserId}, Count: {Count}",
                    currentUserId, count);
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent active users");
                return StatusCode(500, new { message = "An error occurred while retrieving recent active users" });
            }
        }

        [HttpGet("similar")]
        public async Task<ActionResult<List<UserRecommendationResponse>>> GetSimilarUsers(
            [FromQuery] int count = 10)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var recommendations = await _userRecommendationService.GetSimilarUsersAsync(currentUserId, Math.Min(count, 50));
                
                _logger.LogInformation("Similar users retrieved for user: {UserId}, Count: {Count}",
                    currentUserId, count);
                
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving similar users");
                return StatusCode(500, new { message = "An error occurred while retrieving similar users" });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult> RefreshUserRecommendations()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                await _userRecommendationService.RefreshUserRecommendationsAsync(currentUserId);
                
                _logger.LogInformation("User recommendations cache refreshed for user: {UserId}", currentUserId);
                
                return Ok(new { message = "Recommendations refreshed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing user recommendations");
                return StatusCode(500, new { message = "An error occurred while refreshing recommendations" });
            }
        }

        [HttpGet("reasons/{recommendedUserId}")]
        public async Task<ActionResult<List<string>>> GetRecommendationReasons(Guid recommendedUserId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var reasons = await _userRecommendationService.GetRecommendationReasonsAsync(currentUserId, recommendedUserId);
                
                _logger.LogInformation("Recommendation reasons retrieved for user: {UserId} -> {RecommendedUserId}",
                    currentUserId, recommendedUserId);
                
                return Ok(reasons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recommendation reasons");
                return StatusCode(500, new { message = "An error occurred while retrieving recommendation reasons" });
            }
        }

        [HttpGet("explore")]
        public async Task<ActionResult<object>> GetExploreRecommendations()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                
                // Get different types of recommendations for explore page
                var popularUsers = await _userRecommendationService.GetPopularUsersAsync(currentUserId, 5);
                var mutualFollowers = await _userRecommendationService.GetMutualFollowersRecommendationsAsync(currentUserId, 5);
                var recentActive = await _userRecommendationService.GetRecentActiveUsersAsync(currentUserId, 5);
                var similar = await _userRecommendationService.GetSimilarUsersAsync(currentUserId, 5);

                var exploreData = new
                {
                    Popular = popularUsers,
                    MutualFollowers = mutualFollowers,
                    RecentActive = recentActive,
                    Similar = similar
                };
                
                _logger.LogInformation("Explore recommendations retrieved for user: {UserId}", currentUserId);
                
                return Ok(exploreData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving explore recommendations");
                return StatusCode(500, new { message = "An error occurred while retrieving explore recommendations" });
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            return userId;
        }
    }
}
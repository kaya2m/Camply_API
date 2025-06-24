using Camply.Application.Common.Interfaces;
using Camply.Application.Common.Models;
using Camply.Application.Posts.DTOs;
using Camply.Application.Posts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("fixed")]
    public class FeedController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<FeedController> _logger;

        public FeedController(
            IPostService postService,
            ICurrentUserService currentUserService,
            ILogger<FeedController> logger)
        {
            _postService = postService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Ana Sayfa Feed - Takip edilen kullanıcıların gönderileri
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<PostSummaryResponse>), 200)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetHomeFeed(
            [FromQuery, Range(1, 10)] int page = 1,
            [FromQuery, Range(1, 50)] int pageSize = 20)
        {
            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                var feed = await _postService.GetFeedAsync(userId, page, pageSize);

                return Ok(feed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting home feed for user {UserId}", _currentUserService.UserId);
                return StatusCode(500, new { error = "Feed getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Keşfet Feed - Popüler ve trend içerikler
        /// </summary>
        [HttpGet("discover")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResponse<PostSummaryResponse>), 200)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetDiscoverFeed(
            [FromQuery, Range(1, 10)] int page = 1,
            [FromQuery, Range(1, 50)] int pageSize = 20,
            [FromQuery] string sortBy = "popular")
        {
            try
            {
                var posts = await _postService.GetPostsAsync(page, pageSize, sortBy, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discover feed");
                return StatusCode(500, new { error = "Keşfet feed'i getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Yakındaki İçerikler - Lokasyon bazlı
        /// </summary>
        [HttpGet("nearby")]
        [ProducesResponseType(typeof(PagedResponse<PostSummaryResponse>), 200)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetNearbyFeed(
            [FromQuery] Guid? locationId,
            [FromQuery, Range(1, 10)] int page = 1,
            [FromQuery, Range(1, 50)] int pageSize = 20)
        {
            try
            {
                if (!locationId.HasValue)
                {
                    return BadRequest(new { error = "Lokasyon ID gerekli." });
                }

                var posts = await _postService.GetPostsByLocationAsync(locationId.Value, page, pageSize, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby feed for location {LocationId}", locationId);
                return StatusCode(500, new { error = "Yakındaki içerikler getirilirken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Etiket bazlı feed
        /// </summary>
        [HttpGet("tag/{tagName}")]
        [ProducesResponseType(typeof(PagedResponse<PostSummaryResponse>), 200)]
        public async Task<ActionResult<PagedResponse<PostSummaryResponse>>> GetTagFeed(
            string tagName,
            [FromQuery, Range(1, 10)] int page = 1,
            [FromQuery, Range(1, 50)] int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return BadRequest(new { error = "Etiket adı gerekli." });
                }

                var posts = await _postService.GetPostsByTagAsync(tagName, page, pageSize, _currentUserService.UserId);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tag feed for tag {TagName}", tagName);
                return StatusCode(500, new { error = "Etiket feed'i getirilirken bir hata oluştu." });
            }
        }
    }
}
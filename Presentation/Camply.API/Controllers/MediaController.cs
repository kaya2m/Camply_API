using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Camply.Application.Media.DTOs;
using Camply.Application.Media.Interfaces;
using Camply.Application.Common.Models;
using Camply.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/media")]
    public class MediaController : ControllerBase
    {
        private readonly IMediaService _mediaService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<MediaController> _logger;

        public MediaController(
            IMediaService mediaService,
            ICurrentUserService currentUserService,
            ILogger<MediaController> logger)
        {
            _mediaService = mediaService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a single media file
        /// </summary>
        [HttpPost("upload")]
        [Authorize]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MediaUploadResponse>> UploadMedia(
            IFormFile file,
            [FromForm] string description = null,
            [FromForm] string altTag = null)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file provided" });

                var uploadResponse = await _mediaService.UploadMediaAsync(userId.Value, file);

                // Update description and alt tag if provided
                if (!string.IsNullOrEmpty(description) || !string.IsNullOrEmpty(altTag))
                {
                    var updateRequest = new UpdateMediaRequest
                    {
                        Description = description,
                        AltTag = altTag
                    };
                    await _mediaService.UpdateMediaDetailsAsync(uploadResponse.Id, userId.Value, updateRequest);
                }

                return CreatedAtAction(nameof(GetMediaById), new { id = uploadResponse.Id }, uploadResponse);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while uploading the media" });
            }
        }

        /// <summary>
        /// Upload multiple media files
        /// </summary>
        [HttpPost("upload-multiple")]
        [Authorize]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50MB total limit
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<MediaUploadResponse>>> UploadMultipleMedia(List<IFormFile> files)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                if (files == null || files.Count == 0)
                    return BadRequest(new { message = "No files provided" });

                if (files.Count > 10)
                    return BadRequest(new { message = "Maximum 10 files allowed" });

                var uploadedFiles = new List<MediaUploadResponse>();

                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        var uploadResponse = await _mediaService.UploadMediaAsync(userId.Value, file);
                        uploadedFiles.Add(uploadResponse);
                    }
                }

                return CreatedAtAction(nameof(GetUserMedia), new { userId = userId.Value }, uploadedFiles);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple media files");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while uploading the media files" });
            }
        }

        /// <summary>
        /// Upload profile picture with specific sizing
        /// </summary>
        [HttpPost("upload-profile")]
        [Authorize]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit for profile pics
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MediaUploadResponse>> UploadProfilePicture(
            IFormFile file,
            [FromForm] string description = "Profile picture")
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file provided" });

                // Validate image file
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return BadRequest(new { message = "Only JPEG, PNG, and WebP images are allowed for profile pictures" });

                var uploadResponse = await _mediaService.UploadMediaAsync(userId.Value, file);

                // Set as profile image
                await _mediaService.AssignMediaToEntityAsync(uploadResponse.Id, userId.Value, "Profile", userId.Value);

                // Update description
                var updateRequest = new UpdateMediaRequest
                {
                    Description = description,
                    AltTag = $"Profile picture of user {userId.Value}"
                };
                await _mediaService.UpdateMediaDetailsAsync(uploadResponse.Id, userId.Value, updateRequest);

                return CreatedAtAction(nameof(GetMediaById), new { id = uploadResponse.Id }, uploadResponse);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while uploading the profile picture" });
            }
        }

        /// <summary>
        /// Get media by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MediaDetailResponse>> GetMediaById(Guid id)
        {
            try
            {
                var media = await _mediaService.GetMediaByIdAsync(id);
                return Ok(media);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving the media" });
            }
        }

        /// <summary>
        /// Get media by user
        /// </summary>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetUserMedia(
            Guid userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var media = await _mediaService.GetMediaByUserAsync(userId, page, pageSize);
                return Ok(media);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media for user {userId}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving user media" });
            }
        }

        /// <summary>
        /// Get current user's media
        /// </summary>
        [HttpGet("my-media")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetMyMedia(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var media = await _mediaService.GetMediaByUserAsync(userId.Value, page, pageSize);
                return Ok(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user's media");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving your media" });
            }
        }

        /// <summary>
        /// Get media by entity (Post, Blog, etc.)
        /// </summary>
        [HttpGet("entity/{entityId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetEntityMedia(
            Guid entityId,
            [FromQuery] string entityType,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(entityType))
                    return BadRequest(new { message = "Entity type is required" });

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var media = await _mediaService.GetMediaByEntityAsync(entityId, entityType, page, pageSize);
                return Ok(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media for entity {entityId} of type {entityType}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving entity media" });
            }
        }

        /// <summary>
        /// Update media details
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMedia(Guid id, [FromBody] UpdateMediaRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                await _mediaService.UpdateMediaDetailsAsync(id, userId.Value, request);
                return Ok(new { message = "Media updated successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating media with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while updating the media" });
            }
        }

        /// <summary>
        /// Delete media
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMedia(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                await _mediaService.DeleteMediaAsync(id, userId.Value);
                return Ok(new { message = "Media deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting media with ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while deleting the media" });
            }
        }

        /// <summary>
        /// Assign media to an entity (Post, Blog, etc.)
        /// </summary>
        [HttpPost("{id}/assign")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignMediaToEntity(
            Guid id,
            [FromBody] AssignMediaRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                if (request.EntityId == Guid.Empty || string.IsNullOrEmpty(request.EntityType))
                    return BadRequest(new { message = "EntityId and EntityType are required" });

                await _mediaService.AssignMediaToEntityAsync(id, request.EntityId, request.EntityType, userId.Value);
                return Ok(new { message = "Media assigned successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning media {id} to entity");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while assigning the media" });
            }
        }

        /// <summary>
        /// Unassign media from entity
        /// </summary>
        [HttpPost("{id}/unassign")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnassignMediaFromEntity(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                await _mediaService.UnassignMediaFromEntityAsync(id, userId.Value);
                return Ok(new { message = "Media unassigned successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unassigning media {id} from entity");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while unassigning the media" });
            }
        }/// <summary>
         /// Get optimized image URL with specific dimensions
         /// </summary>
        [HttpGet("{id}/optimized")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OptimizedImageResponse>> GetOptimizedImage(
            Guid id,
            [FromQuery] int? width = null,
            [FromQuery] int? height = null,
            [FromQuery] string format = null,
            [FromQuery] int quality = 85)
        {
            try
            {
                var media = await _mediaService.GetMediaByIdAsync(id);

                if (media == null)
                    return NotFound();

                // Only optimize images
                if (!media.FileType.StartsWith(".") ||
                    !new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }.Contains(media.FileType.ToLower()))
                {
                    return Ok(new OptimizedImageResponse { Url = media.Url });
                }

                var cloudflareService = HttpContext.RequestServices.GetService<ICloudflareService>();
                if (cloudflareService != null)
                {
                    var optimizedUrl = await cloudflareService.GetOptimizedImageUrl(
                        media.Url, width, height, format);

                    return Ok(new OptimizedImageResponse
                    {
                        Url = optimizedUrl,
                        OriginalUrl = media.Url,
                        RequestedWidth = width,
                        RequestedHeight = height,
                        RequestedFormat = format,
                        OriginalWidth = media.Width,
                        OriginalHeight = media.Height
                    });
                }

                return Ok(new OptimizedImageResponse { Url = media.Url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting optimized image for media ID {id}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while processing the image" });
            }
        }

        /// <summary>
        /// Get profile picture with different sizes
        /// </summary>
        [HttpGet("profile/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProfileImageResponse>> GetProfileImage(
            Guid userId,
            [FromQuery] string size = "medium") // small, medium, large
        {
            try
            {
                var profileMedia = await _mediaService.GetMediaByEntityAsync(_currentUserService.UserId ?? userId, "Profile", 1, 1);

                if (!profileMedia.Items.Any())
                {
                    return Ok(new ProfileImageResponse
                    {
                        Url = "https://media.thecamply.com/defaults/profile-placeholder.png",
                        IsDefault = true,
                        Size = size
                    });
                }

                var media = profileMedia.Items.First();
                var cloudflareService = HttpContext.RequestServices.GetService<ICloudflareService>();

                if (cloudflareService != null)
                {
                    var (width, height) = GetProfileImageSize(size);
                    var optimizedUrl = await cloudflareService.GetOptimizedImageUrl(
                        media.Url, width, height, "webp");

                    return Ok(new ProfileImageResponse
                    {
                        Url = optimizedUrl,
                        IsDefault = false,
                        Size = size,
                        Width = width,
                        Height = height
                    });
                }

                return Ok(new ProfileImageResponse
                {
                    Url = media.Url,
                    IsDefault = false,
                    Size = size
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting profile image for user {userId}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving the profile image" });
            }
        }

        /// <summary>
        /// Purge CDN cache for a media file
        /// </summary>
        [HttpPost("{id}/purge-cache")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PurgeCache(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User not authenticated" });

                var media = await _mediaService.GetMediaByIdAsync(id);
                if (media.UserId != userId.Value)
                    return Forbid();

                var cloudflareService = HttpContext.RequestServices.GetService<ICloudflareService>();
                if (cloudflareService != null)
                {
                    var success = await cloudflareService.PurgeCache(media.Url);
                    if (success)
                    {
                        return Ok(new { message = "Cache purged successfully" });
                    }
                    return BadRequest(new { message = "Failed to purge cache" });
                }

                return BadRequest(new { message = "CDN service not available" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error purging cache for media {id}");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while purging cache" });
            }
        }

        /// <summary>
        /// Get media analytics
        /// </summary>
        [HttpGet("analytics")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<MediaAnalyticsResponse>> GetMediaAnalytics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var cloudflareService = HttpContext.RequestServices.GetService<ICloudflareService>();
                if (cloudflareService != null)
                {
                    var analytics = await cloudflareService.GetAnalytics(start, end);

                    return Ok(new MediaAnalyticsResponse
                    {
                        StartDate = start,
                        EndDate = end,
                        TotalRequests = analytics.Requests,
                        TotalBandwidth = analytics.Bandwidth,
                        CacheHitRatio = analytics.CacheRatio,
                        StatusCodes = analytics.StatusCodes
                    });
                }

                return Ok(new MediaAnalyticsResponse
                {
                    StartDate = start,
                    EndDate = end,
                    Message = "CDN analytics not available"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media analytics");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving analytics" });
            }
        }

        #region Helper Methods

        private (int width, int height) GetProfileImageSize(string size)
        {
            return size.ToLower() switch
            {
                "small" => (150, 150),
                "medium" => (300, 300),
                "large" => (600, 600),
                _ => (300, 300)
            };
        }

        #endregion
    }

   
}
// Response DTOs
public class OptimizedImageResponse
{
    public string Url { get; set; }
    public string OriginalUrl { get; set; }
    public int? RequestedWidth { get; set; }
    public int? RequestedHeight { get; set; }
    public string RequestedFormat { get; set; }
    public int? OriginalWidth { get; set; }
    public int? OriginalHeight { get; set; }
}


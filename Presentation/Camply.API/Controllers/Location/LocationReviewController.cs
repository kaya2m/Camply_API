using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Camply.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers.Location
{
    [ApiController]
    [Route("api/locations/{locationId}/reviews")]
    public class LocationReviewController : ControllerBase
    {
        private readonly ILocationReviewService _reviewService;
        private readonly ILocationAnalyticsService _analyticsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<LocationReviewController> _logger;

        public LocationReviewController(
            ILocationReviewService reviewService,
            ILocationAnalyticsService analyticsService,
            ICurrentUserService currentUserService,
            ILogger<LocationReviewController> logger)
        {
            _reviewService = reviewService;
            _analyticsService = analyticsService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Gets reviews for a location
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PagedResponse<LocationReviewSummaryResponse>>> GetLocationReviews(
            Guid locationId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var reviews = await _reviewService.GetLocationReviewsAsync(locationId, pageNumber, pageSize, _currentUserService.UserId);
                return Ok(reviews);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reviews for location: {LocationId}", locationId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving reviews" });
            }
        }

        /// <summary>
        /// Gets a specific review
        /// </summary>
        [HttpGet("{reviewId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationReviewDetailResponse>> GetReview(Guid locationId, Guid reviewId)
        {
            try
            {
                var review = await _reviewService.GetReviewByIdAsync(reviewId, _currentUserService.UserId);
                return Ok(review);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review: {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the review" });
            }
        }

        /// <summary>
        /// Creates a new review for a location
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationReviewDetailResponse>> CreateReview(
            Guid locationId,
            [FromBody] CreateLocationReviewRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var review = await _reviewService.CreateReviewAsync(locationId, userId.Value, request);

                // Record interaction for analytics
                await _analyticsService.RecordLocationInteractionAsync(locationId, userId.Value, "review",
                    new Dictionary<string, object> { { "rating", (int)request.OverallRating } });

                return CreatedAtAction(nameof(GetReview), new { locationId, reviewId = review.Id }, review);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review for location: {LocationId}", locationId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the review" });
            }
        }

        /// <summary>
        /// Updates an existing review
        /// </summary>
        [HttpPut("{reviewId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationReviewDetailResponse>> UpdateReview(
            Guid locationId,
            Guid reviewId,
            [FromBody] UpdateLocationReviewRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var review = await _reviewService.UpdateReviewAsync(reviewId, userId.Value, request);
                return Ok(review);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating review: {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the review" });
            }
        }

        /// <summary>
        /// Deletes a review
        /// </summary>
        [HttpDelete("{reviewId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteReview(Guid locationId, Guid reviewId)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _reviewService.DeleteReviewAsync(reviewId, userId.Value);
                return Ok(new { message = "Review deleted successfully" });
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
                _logger.LogError(ex, "Error deleting review: {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the review" });
            }
        }

        /// <summary>
        /// Marks a review as helpful or not helpful
        /// </summary>
        [HttpPost("{reviewId}/helpful")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkReviewHelpful(
            Guid locationId,
            Guid reviewId,
            [FromBody] ReviewHelpfulRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _reviewService.MarkReviewHelpfulAsync(reviewId, userId.Value, request);
                return Ok(new { message = $"Review marked as {(request.IsHelpful ? "helpful" : "not helpful")}" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking review helpful: {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Removes helpful/not helpful marking from a review
        /// </summary>
        [HttpDelete("{reviewId}/helpful")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RemoveReviewHelpful(Guid locationId, Guid reviewId)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _reviewService.RemoveReviewHelpfulAsync(reviewId, userId.Value);
                return Ok(new { message = "Helpful marking removed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing review helpful: {ReviewId}", reviewId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Gets rating breakdown for a location
        /// </summary>
        [HttpGet("rating-breakdown")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationRatingBreakdown>> GetRatingBreakdown(Guid locationId)
        {
            try
            {
                var breakdown = await _reviewService.GetLocationRatingBreakdownAsync(locationId);
                return Ok(breakdown);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating breakdown for location: {LocationId}", locationId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving rating breakdown" });
            }
        }
    }
}


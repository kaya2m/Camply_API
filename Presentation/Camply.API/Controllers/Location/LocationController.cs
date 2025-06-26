using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Camply.Domain.Common;
using Camply.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers.Location
{
    [ApiController]
    [Route("api/locations")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILocationAnalyticsService _analyticsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<LocationController> _logger;

        public LocationController(
            ILocationService locationService,
            ILocationAnalyticsService analyticsService,
            ICurrentUserService currentUserService,
            ILogger<LocationController> logger)
        {
            _locationService = locationService;
            _analyticsService = analyticsService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Gets a paged list of locations with optional filtering
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetLocations(
            [FromQuery] string query = null,
            [FromQuery] double? latitude = null,
            [FromQuery] double? longitude = null,
            [FromQuery] double? radiusKm = null,
            [FromQuery] List<LocationType> types = null,
            [FromQuery] List<string> features = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] double? minRating = null,
            [FromQuery] bool? isSponsored = null,
            [FromQuery] bool? hasEntryFee = null,
            [FromQuery] string sortBy = "distance",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // Validate parameters
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var request = new LocationSearchRequest
                {
                    Query = query,
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusKm = radiusKm,
                    Types = types ?? new List<LocationType>(),
                    Features = features ?? new List<string>(),
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    MinRating = minRating,
                    IsSponsored = isSponsored,
                    HasEntryFee = hasEntryFee,
                    SortBy = sortBy,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };

                var locations = await _locationService.GetLocationsAsync(request, _currentUserService.UserId);

                // Record search metrics for analytics
                if (!string.IsNullOrEmpty(query))
                {
                    await _analyticsService.RecordSearchMetricsAsync(query, features, locations.TotalCount, _currentUserService.UserId);
                }

                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving locations" });
            }
        }

        /// <summary>
        /// Gets a specific location by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationDetailResponse>> GetLocationById(Guid id)
        {
            try
            {
                var location = await _locationService.GetLocationByIdAsync(id, _currentUserService.UserId);
                return Ok(location);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the location" });
            }
        }

        /// <summary>
        /// Creates a new location
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LocationDetailResponse>> CreateLocation([FromBody] CreateLocationRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var location = await _locationService.CreateLocationAsync(userId.Value, request);
                return CreatedAtAction(nameof(GetLocationById), new { id = location.Id }, location);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the location" });
            }
        }

        /// <summary>
        /// Updates an existing location
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationDetailResponse>> UpdateLocation(Guid id, [FromBody] UpdateLocationRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var location = await _locationService.UpdateLocationAsync(id, userId.Value, request);
                return Ok(location);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the location" });
            }
        }

        /// <summary>
        /// Deletes a location
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteLocation(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _locationService.DeleteLocationAsync(id, userId.Value);
                return Ok(new { message = "Location deleted successfully" });
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
                _logger.LogError(ex, "Error deleting location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the location" });
            }
        }

        /// <summary>
        /// Gets sponsored locations
        /// </summary>
        [HttpGet("sponsored")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetSponsoredLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var locations = await _locationService.GetSponsoredLocationsAsync(pageNumber, pageSize, _currentUserService.UserId);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sponsored locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving sponsored locations" });
            }
        }

        /// <summary>
        /// Gets trending locations
        /// </summary>
        [HttpGet("trending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetTrendingLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int days = 7)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var timeRange = TimeSpan.FromDays(Math.Max(1, Math.Min(days, 30)));
                var locations = await _analyticsService.GetTrendingLocationsAsync(pageNumber, pageSize, timeRange);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trending locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving trending locations" });
            }
        }

        /// <summary>
        /// Gets nearby locations
        /// </summary>
        [HttpGet("nearby")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetNearbyLocations(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 10,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                {
                    return BadRequest(new { message = "Invalid coordinates" });
                }

                if (radiusKm <= 0 || radiusKm > 100)
                {
                    return BadRequest(new { message = "Radius must be between 0 and 100 km" });
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var locations = await _locationService.GetNearbyLocationsAsync(latitude, longitude, radiusKm, pageNumber, pageSize, _currentUserService.UserId);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving nearby locations" });
            }
        }

        /// <summary>
        /// Gets locations by type
        /// </summary>
        [HttpGet("by-type/{type}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetLocationsByType(
            LocationType type,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var locations = await _locationService.GetLocationsByTypeAsync(type, pageNumber, pageSize, _currentUserService.UserId);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations by type: {Type}", type);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving locations" });
            }
        }

        /// <summary>
        /// Gets filter options for location search
        /// </summary>
        [HttpGet("filter-options")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<LocationFilterOptions>> GetFilterOptions()
        {
            try
            {
                var options = await _locationService.GetFilterOptionsAsync();
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filter options");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving filter options" });
            }
        }

        /// <summary>
        /// Bookmarks a location
        /// </summary>
        [HttpPost("{id}/bookmark")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BookmarkLocation(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _locationService.BookmarkLocationAsync(id, userId.Value);

                // Record interaction for analytics
                await _analyticsService.RecordLocationInteractionAsync(id, userId.Value, "bookmark");

                return Ok(new { message = "Location bookmarked successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bookmarking location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while bookmarking the location" });
            }
        }

        /// <summary>
        /// Removes bookmark from a location
        /// </summary>
        [HttpDelete("{id}/bookmark")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UnbookmarkLocation(Guid id)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _locationService.UnbookmarkLocationAsync(id, userId.Value);

                // Record interaction for analytics
                await _analyticsService.RecordLocationInteractionAsync(id, userId.Value, "unbookmark");

                return Ok(new { message = "Location unbookmarked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbookmarking location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while unbookmarking the location" });
            }
        }

        /// <summary>
        /// Gets user's bookmarked locations
        /// </summary>
        [HttpGet("bookmarks")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetBookmarkedLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var locations = await _locationService.GetBookmarkedLocationsAsync(userId.Value, pageNumber, pageSize);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bookmarked locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving bookmarked locations" });
            }
        }

        /// <summary>
        /// Gets location statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationStatisticsResponse>> GetLocationStatistics(Guid id)
        {
            try
            {
                var statistics = await _locationService.GetLocationStatisticsAsync(id);
                return Ok(statistics);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location statistics: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving location statistics" });
            }
        }

        /// <summary>
        /// Gets recommended locations for the current user
        /// </summary>
        [HttpGet("recommendations")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetRecommendedLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 50) pageSize = 50;

                var locations = await _analyticsService.GetRecommendedLocationsAsync(userId.Value, pageNumber, pageSize);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommended locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving recommendations" });
            }
        }
    }
}


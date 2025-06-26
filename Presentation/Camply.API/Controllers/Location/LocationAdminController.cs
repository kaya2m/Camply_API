using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camply.API.Controllers.Location
{
    [ApiController]
    [Route("api/admin/locations")]
    [Authorize(Roles = "Admin")]
    public class LocationAdminController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILocationAnalyticsService _analyticsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<LocationAdminController> _logger;

        public LocationAdminController(
            ILocationService locationService,
            ILocationAnalyticsService analyticsService,
            ICurrentUserService currentUserService,
            ILogger<LocationAdminController> logger)
        {
            _locationService = locationService;
            _analyticsService = analyticsService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Gets pending locations for approval
        /// </summary>
        [HttpGet("pending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<LocationSummaryResponse>>> GetPendingLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var locations = await _locationService.GetPendingLocationsAsync(pageNumber, pageSize);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending locations");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving pending locations" });
            }
        }

        /// <summary>
        /// Approves a location
        /// </summary>
        [HttpPost("{id}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveLocation(Guid id, [FromBody] AdminLocationApprovalRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                request.IsApproved = true;
                await _locationService.ApproveLocationAsync(id, userId.Value, request);
                return Ok(new { message = "Location approved successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while approving the location" });
            }
        }

        /// <summary>
        /// Rejects a location
        /// </summary>
        [HttpPost("{id}/reject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RejectLocation(Guid id, [FromBody] AdminLocationApprovalRequest request)
        {
            try
            {
                var userId = _currentUserService.UserId;
                if (!userId.HasValue)
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                request.IsApproved = false;
                await _locationService.RejectLocationAsync(id, userId.Value, request);
                return Ok(new { message = "Location rejected successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while rejecting the location" });
            }
        }

        /// <summary>
        /// Sets sponsorship for a location
        /// </summary>
        [HttpPost("{id}/sponsorship")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetSponsorship(Guid id, [FromBody] SponsorshipRequest request)
        {
            try
            {
                await _locationService.SetSponsorshipAsync(id, request);
                return Ok(new { message = "Sponsorship set successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting sponsorship for location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while setting sponsorship" });
            }
        }

        /// <summary>
        /// Removes sponsorship from a location
        /// </summary>
        [HttpDelete("{id}/sponsorship")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveSponsorship(Guid id)
        {
            try
            {
                await _locationService.RemoveSponsorshipAsync(id);
                return Ok(new { message = "Sponsorship removed successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing sponsorship for location: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while removing sponsorship" });
            }
        }

        /// <summary>
        /// Generates analytics report for a location
        /// </summary>
        [HttpGet("{id}/analytics-report")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LocationAnalyticsReport>> GenerateLocationReport(
            Guid id,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var report = await _analyticsService.GenerateLocationReportAsync(id, start, end);
                return Ok(report);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating location report: {LocationId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while generating the report" });
            }
        }

        /// <summary>
        /// Generates global analytics report
        /// </summary>
        [HttpGet("global-analytics-report")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<GlobalLocationReport>> GenerateGlobalReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var report = await _analyticsService.GenerateGlobalLocationReportAsync(start, end);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating global report");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while generating the report" });
            }
        }
    }
}
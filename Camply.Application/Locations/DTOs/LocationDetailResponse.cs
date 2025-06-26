using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;

namespace Camply.Application.Locations.DTOs
{
    public class LocationDetailResponse : LocationSummaryResponse
    {
        public string ContactPhone { get; set; }
        public string ContactEmail { get; set; }
        public string Website { get; set; }
        public string OpeningHours { get; set; }
        public List<string> AllFeatures { get; set; } = new List<string>();
        public int? MaxCapacity { get; set; }
        public int? MaxVehicles { get; set; }
        public string FacebookUrl { get; set; }
        public string InstagramUrl { get; set; }
        public string TwitterUrl { get; set; }
        public UserSummaryResponse AddedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public List<MediaSummaryResponse> Photos { get; set; } = new List<MediaSummaryResponse>();
        public List<LocationReviewSummaryResponse> RecentReviews { get; set; } = new List<LocationReviewSummaryResponse>();
        public LocationRatingBreakdown RatingBreakdown { get; set; }
        public int TotalVisitCount { get; set; }
    }
}

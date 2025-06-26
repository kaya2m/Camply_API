using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;

namespace Camply.Application.Locations.DTOs
{
    public class LocationReviewSummaryResponse
    {
        public Guid Id { get; set; }
        public UserSummaryResponse User { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int OverallRating { get; set; }
        public int? CleanlinessRating { get; set; }
        public int? ServiceRating { get; set; }
        public int? LocationRating { get; set; }
        public int? ValueRating { get; set; }
        public int? FacilitiesRating { get; set; }
        public bool IsVerified { get; set; }
        public bool IsRecommended { get; set; }
        public DateTime? VisitDate { get; set; }
        public int? StayDuration { get; set; }
        public int HelpfulCount { get; set; }
        public int NotHelpfulCount { get; set; }
        public string OwnerResponse { get; set; }
        public DateTime? OwnerResponseDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<MediaSummaryResponse> Photos { get; set; } = new List<MediaSummaryResponse>();
        public bool IsHelpfulByCurrentUser { get; set; }
        public bool IsNotHelpfulByCurrentUser { get; set; }
    }
}

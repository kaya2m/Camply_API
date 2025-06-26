using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Locations.DTOs
{
    public class CreateLocationReviewRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; }

        [Required]
        [Range(1, 5)]
        public ReviewRating OverallRating { get; set; }

        [Range(1, 5)]
        public ReviewRating? CleanlinessRating { get; set; }

        [Range(1, 5)]
        public ReviewRating? ServiceRating { get; set; }

        [Range(1, 5)]
        public ReviewRating? LocationRating { get; set; }

        [Range(1, 5)]
        public ReviewRating? ValueRating { get; set; }

        [Range(1, 5)]
        public ReviewRating? FacilitiesRating { get; set; }

        public bool IsRecommended { get; set; }

        public DateTime? VisitDate { get; set; }

        [Range(1, 365)]
        public int? StayDuration { get; set; }

        public List<Guid> PhotoIds { get; set; } = new List<Guid>();
    }
}

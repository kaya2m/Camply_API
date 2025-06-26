namespace Camply.Application.Locations.DTOs
{
    public class LocationStatisticsResponse
    {
        public int TotalViews { get; set; }
        public int TotalReviews { get; set; }
        public double AverageRating { get; set; }
        public Dictionary<string, int> MonthlyViews { get; set; } = new Dictionary<string, int>();
        public Dictionary<int, int> RatingDistribution { get; set; } = new Dictionary<int, int>();
        public List<LocationReviewSummaryResponse> RecentReviews { get; set; } = new List<LocationReviewSummaryResponse>();
        public int BookmarkCount { get; set; }
        public int ShareCount { get; set; }
    }
}

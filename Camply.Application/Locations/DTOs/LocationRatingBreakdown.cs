namespace Camply.Application.Locations.DTOs
{
    public class LocationRatingBreakdown
    {
        public double? AverageOverall { get; set; }
        public double? AverageCleanliness { get; set; }
        public double? AverageService { get; set; }
        public double? AverageLocation { get; set; }
        public double? AverageValue { get; set; }
        public double? AverageFacilities { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new Dictionary<int, int>();
        public int TotalReviews { get; set; }
        public int VerifiedReviews { get; set; }
        public int RecommendedCount { get; set; }
    }
}

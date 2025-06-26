namespace Camply.Application.Locations.DTOs
{
    public class LocationAnalyticsReport
    {
        public Guid LocationId { get; set; }
        public string LocationName { get; set; }
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }

        public LocationViewStats ViewStats { get; set; }
        public LocationRatingBreakdown RatingStats { get; set; }

        public int NewReviews { get; set; }
        public int NewBookmarks { get; set; }
        public double AverageSessionDuration { get; set; }
        public double ConversionRate { get; set; } // View to interaction ratio

        public Dictionary<string, int> ReferralSources { get; set; } = new();
        public Dictionary<string, int> UserDemographics { get; set; } = new();
    }
}

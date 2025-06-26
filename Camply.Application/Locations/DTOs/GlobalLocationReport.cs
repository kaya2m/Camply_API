using Camply.Domain.Enums;

namespace Camply.Application.Locations.DTOs
{
    public class GlobalLocationReport
    {
        public DateTime ReportStartDate { get; set; }
        public DateTime ReportEndDate { get; set; }

        public int TotalLocations { get; set; }
        public int ActiveLocations { get; set; }
        public int PendingLocations { get; set; }
        public int SponsoredLocations { get; set; }

        public long TotalViews { get; set; }
        public int TotalReviews { get; set; }
        public double GlobalAverageRating { get; set; }

        public Dictionary<LocationType, int> LocationsByType { get; set; } = new();
        public Dictionary<string, int> LocationsByCountry { get; set; } = new();
        public Dictionary<string, int> TopFeatures { get; set; } = new();

        public List<LocationSummaryResponse> TrendingLocations { get; set; } = new();
        public List<LocationSummaryResponse> TopRatedLocations { get; set; } = new();
    }
}

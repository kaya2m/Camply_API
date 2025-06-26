    
namespace Camply.Application.Locations.DTOs
{
    public class LocationSummaryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public bool IsSponsored { get; set; }
        public int SponsoredPriority { get; set; }
        public bool HasEntryFee { get; set; }
        public decimal? EntryFee { get; set; }
        public string Currency { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string PrimaryImageUrl { get; set; }
        public List<string> MainFeatures { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public double? DistanceKm { get; set; } // Kullanıcının konumuna uzaklık
        public bool IsBookmarkedByCurrentUser { get; set; }
    }
}

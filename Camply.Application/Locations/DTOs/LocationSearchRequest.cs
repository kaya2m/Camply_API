using Camply.Domain.Enums;

namespace Camply.Application.Locations.DTOs
{
    public class LocationSearchRequest
    {
        public string Query { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; } = 10;
        public List<LocationType> Types { get; set; } = new List<LocationType>();
        public List<string> Features { get; set; } = new List<string>();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinRating { get; set; }
        public bool? IsSponsored { get; set; }
        public bool? HasEntryFee { get; set; }
        public string SortBy { get; set; } = "distance"; // distance, rating, price, name, created
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}

using Camply.Domain.Enums;

namespace Camply.Application.Locations.DTOs
{
    public class NearbyLocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusKm { get; set; } = 10;
        public List<LocationType> Types { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = LocationSortOptions.Distance;
    }
}

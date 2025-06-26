namespace Camply.Application.Locations.DTOs
{
    public class LocationFilterOptions
    {
        public List<LocationTypeOption> LocationTypes { get; set; } = new List<LocationTypeOption>();
        public List<FeatureOption> Features { get; set; } = new List<FeatureOption>();
        public PriceRange PriceRange { get; set; }
        public List<string> Countries { get; set; } = new List<string>();
        public List<string> Cities { get; set; } = new List<string>();
    }
}

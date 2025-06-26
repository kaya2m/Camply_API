using Camply.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Locations.DTOs
{
    public class LocationSearchFilters
    {
        public List<LocationType> Types { get; set; } = new();
        public List<string> Features { get; set; } = new();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinRating { get; set; }
        public bool? IsSponsored { get; set; }
        public bool? HasEntryFee { get; set; }
        public bool? IsVerified { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
    }
}

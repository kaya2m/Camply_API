using Camply.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Locations.DTOs
{
    public class CreateLocationRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(2000)]
        public string Description { get; set; }

        [MaxLength(500)]
        public string Address { get; set; }

        [MaxLength(100)]
        public string City { get; set; }

        [MaxLength(100)]
        public string State { get; set; }

        [Required]
        [MaxLength(100)]
        public string Country { get; set; }

        [MaxLength(20)]
        public string PostalCode { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        [Required]
        public LocationType Type { get; set; }

        [MaxLength(50)]
        public string ContactPhone { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public string ContactEmail { get; set; }

        [MaxLength(500)]
        [Url]
        public string Website { get; set; }

        [MaxLength(1000)]
        public string OpeningHours { get; set; }

        public List<string> Features { get; set; } = new List<string>();

        public bool HasEntryFee { get; set; }

        [Range(0, 999999)]
        public decimal? EntryFee { get; set; }

        [MaxLength(200)]
        public string FacebookUrl { get; set; }

        [MaxLength(200)]
        public string InstagramUrl { get; set; }

        [MaxLength(200)]
        public string TwitterUrl { get; set; }

        [Range(1, 10000)]
        public int? MaxCapacity { get; set; }

        [Range(1, 1000)]
        public int? MaxVehicles { get; set; }

        public List<Guid> PhotoIds { get; set; } = new List<Guid>();
    }
}

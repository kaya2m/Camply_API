using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Locations.DTOs
{
    public class SponsorshipRequest
    {
        [Required]
        public Guid LocationId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(1, 10)]
        public int Priority { get; set; }

        [Required]
        [Range(0, 999999)]
        public decimal Amount { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; }
    }
}

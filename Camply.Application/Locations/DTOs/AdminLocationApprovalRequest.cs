using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Locations.DTOs
{
    public class AdminLocationApprovalRequest
    {
        [Required]
        public bool IsApproved { get; set; }

        [MaxLength(500)]
        public string RejectionReason { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; }
    }
}

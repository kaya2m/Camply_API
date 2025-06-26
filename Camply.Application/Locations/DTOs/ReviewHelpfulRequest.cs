using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Locations.DTOs
{
    public class ReviewHelpfulRequest
    {
        [Required]
        public bool IsHelpful { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Locations.DTOs
{
    public class OwnerResponseRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Response { get; set; }
    }
}

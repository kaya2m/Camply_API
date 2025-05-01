using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Media.DTOs
{
    public class UpdateMediaRequest
    {
        [StringLength(255)]
        public string Description { get; set; }

        [StringLength(100)]
        public string AltTag { get; set; }
    }
}

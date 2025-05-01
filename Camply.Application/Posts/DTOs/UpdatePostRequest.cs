using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Posts.DTOs
{
    public class UpdatePostRequest
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Content { get; set; }

        public List<Guid> MediaIds { get; set; } = new List<Guid>();

        public List<string> Tags { get; set; } = new List<string>();

        public Guid? LocationId { get; set; }

        public string LocationName { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Blogs.DTOs
{
    public class UpdateBlogRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 3)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [StringLength(500)]
        public string Summary { get; set; }

        public Guid? FeaturedImageId { get; set; }

        public List<Guid> CategoryIds { get; set; } = new List<Guid>();

        public List<string> Tags { get; set; } = new List<string>();

        public string Status { get; set; } // "Draft", "Published"

        public Guid? LocationId { get; set; }

        public string LocationName { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [StringLength(160)]
        public string MetaDescription { get; set; }

        [StringLength(255)]
        public string MetaKeywords { get; set; }
    }
}

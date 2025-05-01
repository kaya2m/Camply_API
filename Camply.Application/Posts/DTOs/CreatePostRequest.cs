using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Posts.DTOs
{
    public class CreatePostRequest
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

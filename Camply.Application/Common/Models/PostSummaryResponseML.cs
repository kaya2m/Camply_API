using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Common.Models
{
    public class PostSummaryResponseML
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string UserProfileImage { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
        public BlogLocationSummaryResponse Location { get; set; }
        public List<MediaSummaryResponse> Media { get; set; } = new List<MediaSummaryResponse>();
        public List<TagResponse> Tags { get; set; } = new List<TagResponse>();
        public double EngagementScore { get; set; }
        public double PersonalizationScore { get; set; }
    }
}

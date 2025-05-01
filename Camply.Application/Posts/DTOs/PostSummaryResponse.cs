using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;

namespace Camply.Application.Posts.DTOs
{
    public class PostSummaryResponse
    {
        public Guid Id { get; set; }
        public UserSummaryResponse User { get; set; }
        public string Content { get; set; }
        public List<MediaSummaryResponse> Media { get; set; } = new List<MediaSummaryResponse>();
        public DateTime CreatedAt { get; set; }
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }  
        public List<TagResponse> Tags { get; set; } = new List<TagResponse>();
        public LocationSummaryResponse Location { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
    }
}

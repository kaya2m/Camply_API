using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;

namespace Camply.Application.Blogs.DTOs
{
    public class BlogSummaryResponse
    {
        public Guid Id { get; set; }
        public UserSummaryResponse User { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Summary { get; set; }
        public MediaSummaryResponse FeaturedImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int ViewCount { get; set; }
        public List<CategoryResponse> Categories { get; set; } = new List<CategoryResponse>();
        public List<TagResponse> Tags { get; set; } = new List<TagResponse>();
        public BlogLocationSummaryResponse Location { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
    }
}

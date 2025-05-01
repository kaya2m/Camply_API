using Camply.Application.Common.Models;

namespace Camply.Application.Blogs.DTOs
{
    public class BlogDetailResponse : BlogSummaryResponse
    {
        public string Content { get; set; }
        public string Status { get; set; }
        public List<MediaSummaryResponse> Media { get; set; } = new List<MediaSummaryResponse>();
        public List<CommentResponse> Comments { get; set; } = new List<CommentResponse>();
        public List<BlogSummaryResponse> RelatedBlogs { get; set; } = new List<BlogSummaryResponse>();
    }
}

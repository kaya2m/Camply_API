using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;

namespace Camply.Application.Posts.DTOs
{
    public class PostDetailResponse : PostSummaryResponse
    {
        public List<CommentResponse> Comments { get; set; } = new List<CommentResponse>();
        public List<UserSummaryResponse> LikedByUsers { get; set; } = new List<UserSummaryResponse>();
    }
}

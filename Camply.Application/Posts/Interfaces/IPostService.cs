using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Application.Common.Models;
using Camply.Application.Posts.DTOs;

namespace Camply.Application.Posts.Interfaces
{
    public interface IPostService
    {
        Task<PagedResponse<PostSummaryResponse>> GetPostsAsync(int pageNumber, int pageSize, string sortBy = "recent", Guid? currentUserId = null);
        Task<PagedResponse<PostSummaryResponse>> GetPostsByUserAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<PostSummaryResponse>> GetFeedAsync(Guid userId, int pageNumber, int pageSize);
        Task<PagedResponse<PostSummaryResponse>> GetPostsByTagAsync(string tag, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<PostSummaryResponse>> GetPostsByLocationAsync(Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PostDetailResponse> GetPostByIdAsync(Guid postId, Guid? currentUserId = null);
        Task<PostDetailResponse> CreatePostAsync(Guid userId, CreatePostRequest request);
        Task<PostDetailResponse> UpdatePostAsync(Guid postId, Guid userId, UpdatePostRequest request);
        Task<bool> DeletePostAsync(Guid postId, Guid userId);
        Task<bool> LikePostAsync(Guid postId, Guid userId);
        Task<bool> UnlikePostAsync(Guid postId, Guid userId);
        Task<PagedResponse<CommentResponse>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize);
        Task<CommentResponse> AddCommentAsync(Guid postId, Guid userId, CreateCommentRequest request);
        Task<bool> DeleteCommentAsync(Guid commentId, Guid userId);
        Task<PagedResponse<PostSummaryResponse>> SearchPostsAsync(string query, int pageNumber, int pageSize, Guid? currentUserId = null);
    }
}

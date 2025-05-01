using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Application.Blogs.DTOs;
using Camply.Application.Common.Models;

namespace Camply.Application.Blogs.Interfaces
{
    public interface IBlogService
    {
        Task<PagedResponse<BlogSummaryResponse>> GetBlogsAsync(int pageNumber, int pageSize, string sortBy = "recent", Guid? currentUserId = null);
        Task<PagedResponse<BlogSummaryResponse>> GetBlogsByUserAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<BlogSummaryResponse>> GetBlogsByCategoryAsync(Guid categoryId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<BlogSummaryResponse>> GetBlogsByTagAsync(string tag, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<BlogSummaryResponse>> GetBlogsByLocationAsync(Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<BlogDetailResponse> GetBlogByIdAsync(Guid blogId, Guid? currentUserId = null);
        Task<BlogDetailResponse> GetBlogBySlugAsync(string slug, Guid? currentUserId = null);
        Task<BlogDetailResponse> CreateBlogAsync(Guid userId, CreateBlogRequest request);
        Task<BlogDetailResponse> UpdateBlogAsync(Guid blogId, Guid userId, UpdateBlogRequest request);
        Task<bool> DeleteBlogAsync(Guid blogId, Guid userId);
        Task<bool> LikeBlogAsync(Guid blogId, Guid userId);
        Task<bool> UnlikeBlogAsync(Guid blogId, Guid userId);
        Task<PagedResponse<CommentResponse>> GetCommentsAsync(Guid blogId, int pageNumber, int pageSize);
        Task<CommentResponse> AddCommentAsync(Guid blogId, Guid userId, CreateCommentRequest request);
        Task<bool> DeleteCommentAsync(Guid commentId, Guid userId);
        Task<List<CategoryResponse>> GetCategoriesAsync();
        Task<CategoryResponse> GetCategoryByIdAsync(Guid categoryId);
        Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, Guid userId);
        Task<CategoryResponse> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request, Guid userId);
        Task<bool> DeleteCategoryAsync(Guid categoryId, Guid userId);
        Task<PagedResponse<BlogSummaryResponse>> SearchBlogsAsync(string query, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<int> IncrementViewCountAsync(Guid blogId);
    }
}

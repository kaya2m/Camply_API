using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Camply.Application.Blogs.DTOs;
using Camply.Application.Blogs.Interfaces;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Camply.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;
using Camply.Application.Common.Models;

namespace Camply.Infrastructure.Services
{
    public class BlogService : IBlogService
    {
        private readonly IRepository<Blog> _blogRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Media> _mediaRepository;
        private readonly IRepository<Comment> _commentRepository;
        private readonly IRepository<Like> _likeRepository;
        private readonly IRepository<Tag> _tagRepository;
        private readonly IRepository<BlogTag> _blogTagRepository;
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<BlogCategory> _blogCategoryRepository;
        private readonly IRepository<Location> _locationRepository;
        private readonly ILogger<BlogService> _logger;

        public BlogService(
            IRepository<Blog> blogRepository,
            IRepository<User> userRepository,
            IRepository<Media> mediaRepository,
            IRepository<Comment> commentRepository,
            IRepository<Like> likeRepository,
            IRepository<Tag> tagRepository,
            IRepository<BlogTag> blogTagRepository,
            IRepository<Category> categoryRepository,
            IRepository<BlogCategory> blogCategoryRepository,
            IRepository<Location> locationRepository,
            ILogger<BlogService> logger)
        {
            _blogRepository = blogRepository;
            _userRepository = userRepository;
            _mediaRepository = mediaRepository;
            _commentRepository = commentRepository;
            _likeRepository = likeRepository;
            _tagRepository = tagRepository;
            _blogTagRepository = blogTagRepository;
            _categoryRepository = categoryRepository;
            _blogCategoryRepository = blogCategoryRepository;
            _locationRepository = locationRepository;
            _logger = logger;
        }

        public async Task<PagedResponse<BlogSummaryResponse>> GetBlogsAsync(int pageNumber, int pageSize, string sortBy = "recent", Guid? currentUserId = null)
        {
            try
            {
                var query = await _blogRepository.FindAsync(b => b.Status == BlogStatus.Published);
                var blogs = query.ToList();

                // Apply sorting
                blogs = ApplySorting(blogs, sortBy).ToList();

                // Get total count
                var totalCount = blogs.Count;

                // Apply pagination
                var paginatedBlogs = blogs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var blogResponses = await MapBlogsToResponseAsync(paginatedBlogs, currentUserId);

                return new PagedResponse<BlogSummaryResponse>
                {
                    Items = blogResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blogs");
                throw;
            }
        }

        public async Task<PagedResponse<BlogSummaryResponse>> GetBlogsByUserAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // If viewing own blogs, show all statuses
                var query = currentUserId.HasValue && currentUserId.Value == userId
                    ? await _blogRepository.FindAsync(b => b.UserId == userId)
                    : await _blogRepository.FindAsync(b => b.UserId == userId && b.Status == BlogStatus.Published);

                var blogs = query.ToList();

                // Sort by recent
                blogs = blogs.OrderByDescending(b => b.CreatedAt).ToList();

                // Get total count
                var totalCount = blogs.Count;

                // Apply pagination
                var paginatedBlogs = blogs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var blogResponses = await MapBlogsToResponseAsync(paginatedBlogs, currentUserId);

                return new PagedResponse<BlogSummaryResponse>
                {
                    Items = blogResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedResponse<BlogSummaryResponse>> GetBlogsByCategoryAsync(Guid categoryId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    throw new KeyNotFoundException($"Category with ID {categoryId} not found");
                }

                // Get blog IDs in this category
                var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.CategoryId == categoryId);
                var blogIds = blogCategoriesQuery.Select(bc => bc.BlogId).ToList();

                // Get blogs
                var blogsQuery = await _blogRepository.FindAsync(b => blogIds.Contains(b.Id) && b.Status == BlogStatus.Published);
                var blogs = blogsQuery.ToList();

                // Sort by recent
                blogs = blogs.OrderByDescending(b => b.PublishedAt ?? b.CreatedAt).ToList();

                // Get total count
                var totalCount = blogs.Count;

                // Apply pagination
                var paginatedBlogs = blogs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var blogResponses = await MapBlogsToResponseAsync(paginatedBlogs, currentUserId);

                return new PagedResponse<BlogSummaryResponse>
                {
                    Items = blogResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for category ID {categoryId}");
                throw;
            }
        }

        public async Task<PagedResponse<BlogSummaryResponse>> GetBlogsByTagAsync(string tag, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                // Find tag by name or slug
                var queryTag = await _tagRepository.SingleOrDefaultAsync(t => t.Name.ToLower() == tag.ToLower() || t.Slug.ToLower() == tag.ToLower());
                if (queryTag == null)
                {
                    return new PagedResponse<BlogSummaryResponse>
                    {
                        Items = new List<BlogSummaryResponse>(),
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                // Get blog IDs with this tag
                var blogTagsQuery = await _blogTagRepository.FindAsync(bt => bt.TagId == queryTag.Id);
                var blogIds = blogTagsQuery.Select(bt => bt.BlogId).ToList();

                // Get blogs
                var blogsQuery = await _blogRepository.FindAsync(b => blogIds.Contains(b.Id) && b.Status == BlogStatus.Published);
                var blogs = blogsQuery.ToList();

                // Sort by recent
                blogs = blogs.OrderByDescending(b => b.PublishedAt ?? b.CreatedAt).ToList();

                // Get total count
                var totalCount = blogs.Count;

                // Apply pagination
                var paginatedBlogs = blogs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var blogResponses = await MapBlogsToResponseAsync(paginatedBlogs, currentUserId);

                return new PagedResponse<BlogSummaryResponse>
                {
                    Items = blogResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for tag '{tag}'");
                throw;
            }
        }

        public async Task<PagedResponse<BlogSummaryResponse>> GetBlogsByLocationAsync(Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var query = await _blogRepository.FindAsync(b => b.LocationId == locationId && b.Status == BlogStatus.Published);
                var blogs = query.ToList();

                // Sort by recent
                blogs = blogs.OrderByDescending(b => b.PublishedAt ?? b.CreatedAt).ToList();

                // Get total count
                var totalCount = blogs.Count;

                // Apply pagination
                var paginatedBlogs = blogs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var blogResponses = await MapBlogsToResponseAsync(paginatedBlogs, currentUserId);

                return new PagedResponse<BlogSummaryResponse>
                {
                    Items = blogResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blogs for location ID {locationId}");
                throw;
            }
        }

        public async Task<BlogDetailResponse> GetBlogByIdAsync(Guid blogId, Guid? currentUserId = null)
        {
            try
            {
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Check if blog is published or if current user is the author
                if (blog.Status != BlogStatus.Published &&
                    (!currentUserId.HasValue || blog.UserId != currentUserId.Value))
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                var blogResponse = await MapBlogToDetailResponseAsync(blog, currentUserId);
                return blogResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blog with ID {blogId}");
                throw;
            }
        }

        public async Task<BlogDetailResponse> GetBlogBySlugAsync(string slug, Guid? currentUserId = null)
        {
            try
            {
                var blog = await _blogRepository.SingleOrDefaultAsync(b => b.Slug == slug);
                if (blog == null)
                {
                    throw new KeyNotFoundException($"Blog with slug '{slug}' not found");
                }

                // Check if blog is published or if current user is the author
                if (blog.Status != BlogStatus.Published &&
                    (!currentUserId.HasValue || blog.UserId != currentUserId.Value))
                {
                    throw new KeyNotFoundException($"Blog with slug '{slug}' not found");
                }

                var blogResponse = await MapBlogToDetailResponseAsync(blog, currentUserId);
                return blogResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting blog with slug '{slug}'");
                throw;
            }
        }

        public async Task<BlogDetailResponse> CreateBlogAsync(Guid userId, CreateBlogRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Generate slug from title
                var slug = CreateSlug(request.Title);

                // Check if slug already exists
                var existingBlog = await _blogRepository.SingleOrDefaultAsync(b => b.Slug == slug);
                if (existingBlog != null)
                {
                    // Add unique suffix to slug
                    slug = $"{slug}-{DateTime.UtcNow.Ticks.ToString().Substring(10)}";
                }

                // Create blog
                var blog = new Blog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = request.Title,
                    Slug = slug,
                    Content = request.Content,
                    Summary = request.Summary,
                    FeaturedImageId = request.FeaturedImageId,
                    Status = Enum.Parse<BlogStatus>(request.Status, true),
                    CreatedAt = DateTime.UtcNow,
                    PublishedAt = request.Status.ToLower() == "published" ? DateTime.UtcNow : null,
                    LocationId = request.LocationId,
                    LocationName = request.LocationName,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    MetaDescription = request.MetaDescription,
                    MetaKeywords = request.MetaKeywords
                };

                await _blogRepository.AddAsync(blog);
                await _blogRepository.SaveChangesAsync();

                // Process media (if featured image specified)
                if (request.FeaturedImageId.HasValue)
                {
                    var media = await _mediaRepository.GetByIdAsync(request.FeaturedImageId.Value);
                    if (media != null)
                    {
                        // Link media to blog
                        media.EntityId = blog.Id;
                        media.EntityType = "Blog";
                        _mediaRepository.Update(media);
                        await _mediaRepository.SaveChangesAsync();
                    }
                }

                // Process categories
                if (request.CategoryIds != null && request.CategoryIds.Count > 0)
                {
                    foreach (var categoryId in request.CategoryIds)
                    {
                        var category = await _categoryRepository.GetByIdAsync(categoryId);
                        if (category != null)
                        {
                            var blogCategory = new BlogCategory
                            {
                                BlogId = blog.Id,
                                CategoryId = categoryId,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _blogCategoryRepository.AddAsync(blogCategory);
                        }
                    }
                    await _blogCategoryRepository.SaveChangesAsync();
                }

                // Process tags
                if (request.Tags != null && request.Tags.Count > 0)
                {
                    await ProcessBlogTagsAsync(blog.Id, request.Tags);
                }

                // Return blog with details
                return await GetBlogByIdAsync(blog.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating blog");
                throw;
            }
        }

        public async Task<BlogDetailResponse> UpdateBlogAsync(Guid blogId, Guid userId, UpdateBlogRequest request)
        {
            try
            {
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Check if user is the owner
                if (blog.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this blog");
                }

                // Check if status is changing from Draft to Published
                bool isPublishing = blog.Status == BlogStatus.Draft && request.Status.ToLower() == "published";

                // Update blog
                blog.Title = request.Title;
                blog.Content = request.Content;
                blog.Summary = request.Summary;
                blog.FeaturedImageId = request.FeaturedImageId;
                blog.Status = Enum.Parse<BlogStatus>(request.Status, true);
                blog.LastModifiedAt = DateTime.UtcNow;
                blog.PublishedAt = isPublishing ? DateTime.UtcNow : blog.PublishedAt;
                blog.LocationId = request.LocationId;
                blog.LocationName = request.LocationName;
                blog.Latitude = request.Latitude;
                blog.Longitude = request.Longitude;
                blog.MetaDescription = request.MetaDescription;
                blog.MetaKeywords = request.MetaKeywords;

                _blogRepository.Update(blog);
                await _blogRepository.SaveChangesAsync();

                // Update featured image
                if (request.FeaturedImageId.HasValue)
                {
                    var media = await _mediaRepository.GetByIdAsync(request.FeaturedImageId.Value);
                    if (media != null && (media.EntityId == null || media.EntityId != blogId))
                    {
                        media.EntityId = blog.Id;
                        media.EntityType = "Blog";
                        _mediaRepository.Update(media);
                        await _mediaRepository.SaveChangesAsync();
                    }
                }

                // Update categories
                // First, remove existing categories
                var existingCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.BlogId == blogId);
                var existingCategories = existingCategoriesQuery.ToList();

                foreach (var category in existingCategories)
                {
                    _blogCategoryRepository.Remove(category);
                }
                await _blogCategoryRepository.SaveChangesAsync();

                // Add new categories
                if (request.CategoryIds != null && request.CategoryIds.Count > 0)
                {
                    foreach (var categoryId in request.CategoryIds)
                    {
                        var category = await _categoryRepository.GetByIdAsync(categoryId);
                        if (category != null)
                        {
                            var blogCategory = new BlogCategory
                            {
                                BlogId = blog.Id,
                                CategoryId = categoryId,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _blogCategoryRepository.AddAsync(blogCategory);
                        }
                    }
                    await _blogCategoryRepository.SaveChangesAsync();
                }

                // Update tags
                // First, remove existing tags
                var existingTagsQuery = await _blogTagRepository.FindAsync(bt => bt.BlogId == blogId);
                var existingTags = existingTagsQuery.ToList();

                foreach (var tag in existingTags)
                {
                    _blogTagRepository.Remove(tag);
                }
                await _blogTagRepository.SaveChangesAsync();

                // Add new tags
                if (request.Tags != null && request.Tags.Count > 0)
                {
                    await ProcessBlogTagsAsync(blog.Id, request.Tags);
                }

                // Return updated blog
                return await GetBlogByIdAsync(blog.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating blog with ID {blogId}");
                throw;
            }
        }

        public async Task<bool> DeleteBlogAsync(Guid blogId, Guid userId)
        {
            try
            {
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Check if user is the owner
                if (blog.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to delete this blog");
                }

                // Soft delete
                blog.IsDeleted = true;
                blog.DeletedAt = DateTime.UtcNow;
                blog.DeletedBy = userId;
                blog.Status = BlogStatus.Archived;

                _blogRepository.Update(blog);
                await _blogRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting blog with ID {blogId}");
                throw;
            }
        }

        public async Task<bool> LikeBlogAsync(Guid blogId, Guid userId)
        {
            try
            {
                // Check if blog exists
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null || blog.Status != BlogStatus.Published)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Check if already liked
                var existingLike = await _likeRepository.SingleOrDefaultAsync(
                    l => l.EntityId == blogId && l.UserId == userId && l.EntityType == "Blog");

                if (existingLike != null)
                {
                    // Already liked
                    return true;
                }

                // Create like
                var like = new Like
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EntityId = blogId,
                    EntityType = "Blog",
                    CreatedAt = DateTime.UtcNow
                };

                await _likeRepository.AddAsync(like);
                await _likeRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error liking blog with ID {blogId}");
                throw;
            }
        }

        public async Task<bool> UnlikeBlogAsync(Guid blogId, Guid userId)
        {
            try
            {
                // Find like
                var like = await _likeRepository.SingleOrDefaultAsync(
                    l => l.EntityId == blogId && l.UserId == userId && l.EntityType == "Blog");

                if (like == null)
                {
                    // Not liked
                    return true;
                }

                // Remove like
                _likeRepository.Remove(like);
                await _likeRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unliking blog with ID {blogId}");
                throw;
            }
        }

        public async Task<PagedResponse<CommentResponse>> GetCommentsAsync(Guid blogId, int pageNumber, int pageSize)
        {
            try
            {
                // Check if blog exists
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null || blog.Status != BlogStatus.Published)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Get root comments (those without a parent)
                var commentsQuery = await _commentRepository.FindAsync(
                    c => c.EntityId == blogId && c.EntityType == "Blog" && c.ParentId == null && !c.IsDeleted);
                var comments = commentsQuery.ToList();

                // Sort by recent
                comments = comments.OrderByDescending(c => c.CreatedAt).ToList();

                // Get total count
                var totalCount = comments.Count;

                // Apply pagination
                var paginatedComments = comments
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var commentResponses = await MapCommentsToResponseAsync(paginatedComments);

                return new PagedResponse<CommentResponse>
                {
                    Items = commentResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting comments for blog ID {blogId}");
                throw;
            }
        }

        public async Task<CommentResponse> AddCommentAsync(Guid blogId, Guid userId, CreateCommentRequest request)
        {
            try
            {
                // Check if blog exists
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null || blog.Status != BlogStatus.Published)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Check if user exists
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // If it's a reply, check if parent comment exists
                if (request.ParentId.HasValue)
                {
                    var parentComment = await _commentRepository.GetByIdAsync(request.ParentId.Value);
                    if (parentComment == null || parentComment.IsDeleted)
                    {
                        throw new KeyNotFoundException($"Parent comment with ID {request.ParentId.Value} not found");
                    }

                    // Check if parent comment belongs to the same blog
                    if (parentComment.EntityId != blogId || parentComment.EntityType != "Blog")
                    {
                        throw new InvalidOperationException("Parent comment does not belong to this blog");
                    }
                }

                // Create comment
                var comment = new Comment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Content = request.Content,
                    EntityId = blogId,
                    EntityType = "Blog",
                    ParentId = request.ParentId,
                    Status = CommentStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };

                await _commentRepository.AddAsync(comment);
                await _commentRepository.SaveChangesAsync();

                // Get user details for response
                var commentResponse = new CommentResponse
                {
                    Id = comment.Id,
                    UserId = user.Id,
                    Username = user.Username,
                    UserProfileImage = user.ProfileImageUrl,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    ParentId = comment.ParentId,
                    Replies = new List<CommentResponse>()
                };

                return commentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding comment to blog ID {blogId}");
                throw;
            }
        }

        public async Task<bool> DeleteCommentAsync(Guid commentId, Guid userId)
        {
            try
            {
                var comment = await _commentRepository.GetByIdAsync(commentId);
                if (comment == null)
                {
                    throw new KeyNotFoundException($"Comment with ID {commentId} not found");
                }

                // Check if user is the owner of the comment
                if (comment.UserId != userId)
                {
                    // Check if user is the owner of the blog
                    if (comment.EntityType == "Blog")
                    {
                        var blog = await _blogRepository.GetByIdAsync(comment.EntityId);
                        if (blog.UserId != userId)
                        {
                            throw new UnauthorizedAccessException("You are not authorized to delete this comment");
                        }
                    }
                    else
                    {
                        throw new UnauthorizedAccessException("You are not authorized to delete this comment");
                    }
                }

                // Soft delete
                comment.IsDeleted = true;
                comment.DeletedAt = DateTime.UtcNow;
                comment.DeletedBy = userId;
                comment.Status = CommentStatus.Deleted;

                _commentRepository.Update(comment);
                await _commentRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting comment with ID {commentId}");
                throw;
            }
        }

        public async Task<List<CategoryResponse>> GetCategoriesAsync()
        {
            try
            {
                var categoriesQuery = await _categoryRepository.GetAllAsync();
                var categories = categoriesQuery.ToList();

                var categoryResponses = new List<CategoryResponse>();
                foreach (var category in categories)
                {
                    // Count blogs in this category
                    var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.CategoryId == category.Id);
                    var blogIds = blogCategoriesQuery.Select(bc => bc.BlogId).ToList();

                    var blogsCount = 0;
                    if (blogIds.Any())
                    {
                        blogsCount = (await _blogRepository.FindAsync(b =>
                            blogIds.Contains(b.Id) &&
                            b.Status == BlogStatus.Published &&
                            !b.IsDeleted)).Count();
                    }

                    categoryResponses.Add(new CategoryResponse
                    {
                        Id = category.Id,
                        Name = category.Name,
                        Slug = category.Slug,
                        Description = category.Description,
                        ImageUrl = category.ImageUrl,
                        BlogsCount = blogsCount
                    });
                }

                // Sort by name
                categoryResponses = categoryResponses.OrderBy(c => c.Name).ToList();

                return categoryResponses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                throw;
            }
        }

        public async Task<CategoryResponse> GetCategoryByIdAsync(Guid categoryId)
        {
            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    throw new KeyNotFoundException($"Category with ID {categoryId} not found");
                }

                // Count blogs in this category
                var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.CategoryId == categoryId);
                var blogIds = blogCategoriesQuery.Select(bc => bc.BlogId).ToList();

                var blogsCount = 0;
                if (blogIds.Any())
                {
                    blogsCount = (await _blogRepository.FindAsync(b =>
                        blogIds.Contains(b.Id) &&
                        b.Status == BlogStatus.Published &&
                        !b.IsDeleted)).Count();
                }

                return new CategoryResponse
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    Description = category.Description,
                    ImageUrl = category.ImageUrl,
                    BlogsCount = blogsCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting category with ID {categoryId}");
                throw;
            }
        }

        public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, Guid userId)
        {
            try
            {
                // Generate slug from name
                var slug = CreateSlug(request.Name);

                // Check if slug already exists
                var existingCategory = await _categoryRepository.SingleOrDefaultAsync(c => c.Slug == slug);
                if (existingCategory != null)
                {
                    // Add unique suffix to slug
                    slug = $"{slug}-{DateTime.UtcNow.Ticks.ToString().Substring(10)}";
                }

                // Create category
                var category = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Slug = slug,
                    Description = request.Description,
                    ImageUrl = request.ImageUrl,
                    ParentId = request.ParentId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                await _categoryRepository.AddAsync(category);
                await _categoryRepository.SaveChangesAsync();

                return new CategoryResponse
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    Description = category.Description,
                    ImageUrl = category.ImageUrl,
                    BlogsCount = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                throw;
            }
        }

        public async Task<CategoryResponse> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request, Guid userId)
        {
            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    throw new KeyNotFoundException($"Category with ID {categoryId} not found");
                }

                // Update category
                category.Name = request.Name;
                category.Description = request.Description;
                category.ImageUrl = request.ImageUrl;
                category.ParentId = request.ParentId;
                category.LastModifiedAt = DateTime.UtcNow;
                category.LastModifiedBy = userId;

                _categoryRepository.Update(category);
                await _categoryRepository.SaveChangesAsync();

                // Count blogs in this category
                var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.CategoryId == categoryId);
                var blogIds = blogCategoriesQuery.Select(bc => bc.BlogId).ToList();

                var blogsCount = 0;
                if (blogIds.Any())
                {
                    blogsCount = (await _blogRepository.FindAsync(b =>
                        blogIds.Contains(b.Id) &&
                        b.Status == BlogStatus.Published &&
                        !b.IsDeleted)).Count();
                }

                return new CategoryResponse
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    Description = category.Description,
                    ImageUrl = category.ImageUrl,
                    BlogsCount = blogsCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating category with ID {categoryId}");
                throw;
            }
        }

        public async Task<bool> DeleteCategoryAsync(Guid categoryId, Guid userId)
        {
            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    throw new KeyNotFoundException($"Category with ID {categoryId} not found");
                }

                // Check if category has child categories
                var childCategoriesQuery = await _categoryRepository.FindAsync(c => c.ParentId == categoryId);
                if (childCategoriesQuery.Any())
                {
                    throw new InvalidOperationException("Cannot delete category with child categories");
                }

                // Check if category has blogs
                var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.CategoryId == categoryId);
                if (blogCategoriesQuery.Any())
                {
                    throw new InvalidOperationException("Cannot delete category with associated blogs");
                }

                // Delete category
                _categoryRepository.Remove(category);
                await _categoryRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting category with ID {categoryId}");
                throw;
            }
        }

        public async Task<PagedResponse<BlogSummaryResponse>> SearchBlogsAsync(string query, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    throw new ArgumentException("Search query cannot be empty");
                }

                // Search in blog title, content, and summary
                var blogsQuery = await _blogRepository.FindAsync(b =>
                    b.Status == BlogStatus.Published &&
                    (b.Title.Contains(query) ||
                     b.Content.Contains(query) ||
                     b.Summary.Contains(query) ||
                     b.LocationName.Contains(query)));
                var blogs = blogsQuery.ToList();

                // Search in tags
                var tagsQuery = await _tagRepository.FindAsync(t =>
                    t.Name.Contains(query) || t.Slug.Contains(query));
                var tagIds = tagsQuery.Select(t => t.Id).ToList();

                if (tagIds.Any())
                {
                    var blogTagsQuery = await _blogTagRepository.FindAsync(bt => tagIds.Contains(bt.TagId));
                    var blogIds = blogTagsQuery.Select(bt => bt.BlogId).ToList();

                    if (blogIds.Any())
                    {
                        var taggedBlogsQuery = await _blogRepository.FindAsync(b =>
                            blogIds.Contains(b.Id) && b.Status == BlogStatus.Published);
                        var taggedBlogs = taggedBlogsQuery.ToList();

                        // Merge and remove duplicates
                        blogs = blogs.Union(taggedBlogs, new BlogComparer()).ToList();
                    }
                }

                // Search in categories
                var categoriesQuery = await _categoryRepository.FindAsync(c =>
                    c.Name.Contains(query) || c.Slug.Contains(query));
                var categoryIds = categoriesQuery.Select(c => c.Id).ToList();

                if (categoryIds.Any())
                {
                    var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => categoryIds.Contains(bc.CategoryId));
                    var blogIds = blogCategoriesQuery.Select(bc => bc.BlogId).ToList();

                    if (blogIds.Any())
                    {
                        var categoryBlogsQuery = await _blogRepository.FindAsync(b =>
                            blogIds.Contains(b.Id) && b.Status == BlogStatus.Published);
                        var categoryBlogs = categoryBlogsQuery.ToList();

                        // Merge and remove duplicates
                        blogs = blogs.Union(categoryBlogs, new BlogComparer()).ToList();
                    }
                }

                // Sort by recent
                blogs = blogs.OrderByDescending(b => b.PublishedAt ?? b.CreatedAt).ToList();

                // Get total count
                var totalCount = blogs.Count;

                // Apply pagination
                var paginatedBlogs = blogs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var blogResponses = await MapBlogsToResponseAsync(paginatedBlogs, currentUserId);

                return new PagedResponse<BlogSummaryResponse>
                {
                    Items = blogResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching blogs for query '{query}'");
                throw;
            }
        }

        public async Task<int> IncrementViewCountAsync(Guid blogId)
        {
            try
            {
                var blog = await _blogRepository.GetByIdAsync(blogId);
                if (blog == null || blog.Status != BlogStatus.Published)
                {
                    throw new KeyNotFoundException($"Blog with ID {blogId} not found");
                }

                // Increment view count
                blog.ViewCount++;

                _blogRepository.Update(blog);
                await _blogRepository.SaveChangesAsync();

                return blog.ViewCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error incrementing view count for blog ID {blogId}");
                throw;
            }
        }

        #region Helper Methods

        private IEnumerable<Blog> ApplySorting(IEnumerable<Blog> blogs, string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "recent" => blogs.OrderByDescending(b => b.PublishedAt ?? b.CreatedAt),
                "popular" => blogs.OrderByDescending(b => b.ViewCount),
                "comments" => blogs.OrderByDescending(b => b.Comments.Count),
                "likes" => blogs.OrderByDescending(b => b.Likes.Count),
                _ => blogs.OrderByDescending(b => b.PublishedAt ?? b.CreatedAt)
            };
        }

        private async Task<List<BlogSummaryResponse>> MapBlogsToResponseAsync(List<Blog> blogs, Guid? currentUserId = null)
        {
            var blogResponses = new List<BlogSummaryResponse>();

            foreach (var blog in blogs)
            {
                var blogResponse = await MapBlogToSummaryResponseAsync(blog, currentUserId);
                blogResponses.Add(blogResponse);
            }

            return blogResponses;
        }

        private async Task<BlogSummaryResponse> MapBlogToSummaryResponseAsync(Blog blog, Guid? currentUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(blog.UserId);

            Media featuredImage = null;
            if (blog.FeaturedImageId.HasValue)
            {
                featuredImage = await _mediaRepository.GetByIdAsync(blog.FeaturedImageId.Value);
            }

            // Get likes count
            var likesCount = (await _likeRepository.FindAsync(l => l.EntityId == blog.Id && l.EntityType == "Blog")).Count();

            // Get comments count
            var commentsCount = (await _commentRepository.FindAsync(c => c.EntityId == blog.Id && c.EntityType == "Blog" && !c.IsDeleted)).Count();

            // Check if current user liked this blog
            var isLiked = false;
            if (currentUserId.HasValue)
            {
                isLiked = await _likeRepository.ExistsAsync(l =>
                    l.EntityId == blog.Id && l.UserId == currentUserId.Value && l.EntityType == "Blog");
            }

            // Get categories
            var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc => bc.BlogId == blog.Id);
            var categoryIds = blogCategoriesQuery.Select(bc => bc.CategoryId).ToList();
            var categories = new List<CategoryResponse>();

            if (categoryIds.Any())
            {
                var categoriesQuery = await _categoryRepository.FindAsync(c => categoryIds.Contains(c.Id));
                categories = categoriesQuery.Select(c => new CategoryResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    BlogsCount = 0 // We don't need this count for this response
                }).ToList();
            }

            // Get tags
            var blogTagsQuery = await _blogTagRepository.FindAsync(bt => bt.BlogId == blog.Id);
            var tagIds = blogTagsQuery.Select(bt => bt.TagId).ToList();
            var tags = new List<TagResponse>();

            if (tagIds.Any())
            {
                var tagsQuery = await _tagRepository.FindAsync(t => tagIds.Contains(t.Id));
                tags = tagsQuery.Select(t => new TagResponse
                {
                    Id = t.Id,
                    Name = t.Name,
                    Slug = t.Slug,
                    UsageCount = t.UsageCount
                }).ToList();
            }

            // Get location
            LocationSummaryResponse location = null;
            if (blog.LocationId.HasValue)
            {
                var locationEntity = await _locationRepository.GetByIdAsync(blog.LocationId.Value);
                if (locationEntity != null)
                {
                    location = new LocationSummaryResponse
                    {
                        Id = locationEntity.Id,
                        Name = locationEntity.Name,
                        Latitude = locationEntity.Latitude,
                        Longitude = locationEntity.Longitude,
                        Type = locationEntity.Type.ToString()
                    };
                }
                else if (!string.IsNullOrEmpty(blog.LocationName))
                {
                    location = new LocationSummaryResponse
                    {
                        Id = null,
                        Name = blog.LocationName,
                        Latitude = blog.Latitude,
                        Longitude = blog.Longitude,
                        Type = null
                    };
                }
            }

            return new BlogSummaryResponse
            {
                Id = blog.Id,
                User = new Application.Users.DTOs.UserSummaryResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfileImageUrl = user.ProfileImageUrl
                },
                Title = blog.Title,
                Slug = blog.Slug,
                Summary = blog.Summary,
                FeaturedImage = featuredImage != null ? new MediaSummaryResponse
                {
                    Id = featuredImage.Id,
                    Url = featuredImage.Url,
                    ThumbnailUrl = featuredImage.ThumbnailUrl,
                    FileType = featuredImage.FileType,
                    Description = featuredImage.Description,
                    AltTag = featuredImage.AltTag,
                    Width = featuredImage.Width,
                    Height = featuredImage.Height
                } : null,
                CreatedAt = blog.CreatedAt,
                PublishedAt = blog.PublishedAt,
                LikesCount = likesCount,
                CommentsCount = commentsCount,
                ViewCount = blog.ViewCount,
                Categories = categories,
                Tags = tags,
                Location = location,
                IsLikedByCurrentUser = isLiked
            };
        }

        private async Task<BlogDetailResponse> MapBlogToDetailResponseAsync(Blog blog, Guid? currentUserId = null)
        {
            // Get summary first for common properties
            var summary = await MapBlogToSummaryResponseAsync(blog, currentUserId);

            // Get media
            var mediaQuery = await _mediaRepository.FindAsync(m => m.EntityId == blog.Id && m.EntityType == "Blog");
            var media = mediaQuery.ToList();

            // Get comments (only root comments, replies will be nested)
            var commentsQuery = await _commentRepository.FindAsync(
                c => c.EntityId == blog.Id && c.EntityType == "Blog" && c.ParentId == null && !c.IsDeleted);
            var comments = commentsQuery.ToList();

            // Sort comments by newest first
            comments = comments.OrderByDescending(c => c.CreatedAt).ToList();

            var commentResponses = await MapCommentsToResponseAsync(comments);

            // Get related blogs (same category or tags, limit to 5)
            var relatedBlogs = new List<BlogSummaryResponse>();

            // If we have categories, find blogs in same categories
            if (summary.Categories.Any())
            {
                var categoryIds = summary.Categories.Select(c => c.Id).ToList();
                var blogCategoriesQuery = await _blogCategoryRepository.FindAsync(bc =>
                    categoryIds.Contains(bc.CategoryId) && bc.BlogId != blog.Id);

                var relatedBlogIds = blogCategoriesQuery
                    .Select(bc => bc.BlogId)
                    .Distinct()
                    .Take(5)
                    .ToList();

                if (relatedBlogIds.Any())
                {
                    var relatedBlogsQuery = await _blogRepository.FindAsync(b =>
                        relatedBlogIds.Contains(b.Id) &&
                        b.Status == BlogStatus.Published);

                    var relatedBlogEntities = relatedBlogsQuery
                        .OrderByDescending(b => b.PublishedAt ?? b.CreatedAt)
                        .Take(5)
                        .ToList();

                    if (relatedBlogEntities.Any())
                    {
                        relatedBlogs = await MapBlogsToResponseAsync(relatedBlogEntities, currentUserId);
                    }
                }
            }

            // If we don't have enough related blogs from categories, try tags
            if (relatedBlogs.Count < 5 && summary.Tags.Any())
            {
                var tagIds = summary.Tags.Select(t => t.Id).ToList();
                var blogTagsQuery = await _blogTagRepository.FindAsync(bt =>
                    tagIds.Contains(bt.TagId) && bt.BlogId != blog.Id);

                var relatedBlogIds = blogTagsQuery
                    .Select(bt => bt.BlogId)
                    .Distinct()
                    .Take(5 - relatedBlogs.Count)
                    .ToList();

                if (relatedBlogIds.Any())
                {
                    var relatedBlogsQuery = await _blogRepository.FindAsync(b =>
                        relatedBlogIds.Contains(b.Id) &&
                        b.Status == BlogStatus.Published);

                    var relatedBlogEntities = relatedBlogsQuery
                        .OrderByDescending(b => b.PublishedAt ?? b.CreatedAt)
                        .Take(5 - relatedBlogs.Count)
                        .ToList();

                    if (relatedBlogEntities.Any())
                    {
                        var tagRelatedBlogs = await MapBlogsToResponseAsync(relatedBlogEntities, currentUserId);
                        relatedBlogs.AddRange(tagRelatedBlogs);
                    }
                }
            }

            // Create detail response
            var detailResponse = new BlogDetailResponse
            {
                Id = summary.Id,
                User = summary.User,
                Title = summary.Title,
                Slug = summary.Slug,
                Content = blog.Content,
                Summary = summary.Summary,
                FeaturedImage = summary.FeaturedImage,
                Status = blog.Status.ToString(),
                CreatedAt = summary.CreatedAt,
                PublishedAt = summary.PublishedAt,
                LikesCount = summary.LikesCount,
                CommentsCount = summary.CommentsCount,
                ViewCount = summary.ViewCount,
                Categories = summary.Categories,
                Tags = summary.Tags,
                Location = summary.Location,
                IsLikedByCurrentUser = summary.IsLikedByCurrentUser,
                Media = media.Select(m => new MediaSummaryResponse
                {
                    Id = m.Id,
                    Url = m.Url,
                    ThumbnailUrl = m.ThumbnailUrl,
                    FileType = m.FileType,
                    Description = m.Description,
                    AltTag = m.AltTag,
                    Width = m.Width,
                    Height = m.Height
                }).ToList(),
                Comments = commentResponses,
                RelatedBlogs = relatedBlogs
            };

            return detailResponse;
        }

        private async Task<List<CommentResponse>> MapCommentsToResponseAsync(List<Comment> comments)
        {
            var commentResponses = new List<CommentResponse>();

            foreach (var comment in comments)
            {
                var user = await _userRepository.GetByIdAsync(comment.UserId);

                var commentResponse = new CommentResponse
                {
                    Id = comment.Id,
                    UserId = comment.UserId,
                    Username = user?.Username,
                    UserProfileImage = user?.ProfileImageUrl,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    ParentId = comment.ParentId,
                    Replies = new List<CommentResponse>()
                };

                // Get replies for this comment
                var repliesQuery = await _commentRepository.FindAsync(
                    c => c.ParentId == comment.Id && !c.IsDeleted);
                var replies = repliesQuery.OrderBy(c => c.CreatedAt).ToList();

                if (replies.Any())
                {
                    commentResponse.Replies = await MapCommentsToResponseAsync(replies);
                }

                commentResponses.Add(commentResponse);
            }

            return commentResponses;
        }

        private async Task ProcessBlogTagsAsync(Guid blogId, List<string> tagNames)
        {
            if (tagNames == null || !tagNames.Any())
                return;

            foreach (var tagName in tagNames.Distinct())
            {
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                // Normalize tag name
                var normalizedTagName = tagName.Trim().ToLower();
                if (string.IsNullOrWhiteSpace(normalizedTagName))
                    continue;

                // Create slug
                var slug = CreateSlug(normalizedTagName);

                // Find existing tag or create new one
                var tag = await _tagRepository.SingleOrDefaultAsync(t =>
                    t.Name.ToLower() == normalizedTagName || t.Slug == slug);

                if (tag == null)
                {
                    tag = new Tag
                    {
                        Id = Guid.NewGuid(),
                        Name = normalizedTagName,
                        Slug = slug,
                        UsageCount = 1,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _tagRepository.AddAsync(tag);
                    await _tagRepository.SaveChangesAsync();
                }
                else
                {
                    // Increment usage count
                    tag.UsageCount++;
                    _tagRepository.Update(tag);
                    await _tagRepository.SaveChangesAsync();
                }

                // Create blog-tag relationship
                var blogTag = new BlogTag
                {
                    BlogId = blogId,
                    TagId = tag.Id,
                    CreatedAt = DateTime.UtcNow
                };

                await _blogTagRepository.AddAsync(blogTag);
            }

            await _blogTagRepository.SaveChangesAsync();
        }

        private string CreateSlug(string text)
        {
            // Remove accents and convert to lowercase
            string slug = text.ToLowerInvariant();

            // Remove invalid chars
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

            // Convert spaces to hyphens
            slug = Regex.Replace(slug, @"\s+", "-");

            // Remove multiple hyphens
            slug = Regex.Replace(slug, @"-+", "-");

            // Trim hyphens from ends
            slug = slug.Trim('-');

            return slug;
        }
    }

    public class BlogComparer : IEqualityComparer<Blog>
    {
        public bool Equals(Blog x, Blog y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(Blog obj)
        {
            return obj.Id.GetHashCode();
        }
    }
    #endregion
}
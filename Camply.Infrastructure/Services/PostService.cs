using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Camply.Application.Posts.DTOs;
using Camply.Application.Posts.Interfaces;
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
    public class PostService : IPostService
    {
        private readonly IRepository<Post> _postRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Media> _mediaRepository;
        private readonly IRepository<Comment> _commentRepository;
        private readonly IRepository<Like> _likeRepository;
        private readonly IRepository<Tag> _tagRepository;
        private readonly IRepository<PostTag> _postTagRepository;
        private readonly IRepository<Follow> _followRepository;
        private readonly IRepository<Location> _locationRepository;
        private readonly ILogger<PostService> _logger;

        public PostService(
            IRepository<Post> postRepository,
            IRepository<User> userRepository,
            IRepository<Media> mediaRepository,
            IRepository<Comment> commentRepository,
            IRepository<Like> likeRepository,
            IRepository<Tag> tagRepository,
            IRepository<PostTag> postTagRepository,
            IRepository<Follow> followRepository,
            IRepository<Location> locationRepository,
            ILogger<PostService> logger)
        {
            _postRepository = postRepository;
            _userRepository = userRepository;
            _mediaRepository = mediaRepository;
            _commentRepository = commentRepository;
            _likeRepository = likeRepository;
            _tagRepository = tagRepository;
            _postTagRepository = postTagRepository;
            _followRepository = followRepository;
            _locationRepository = locationRepository;
            _logger = logger;
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsAsync(int pageNumber, int pageSize, string sortBy = "recent", Guid? currentUserId = null)
        {
            try
            {
                var query = await _postRepository.FindAsync(p => p.Status == PostStatus.Active);
                var posts = query.ToList();

                // Apply sorting
                posts = ApplySorting(posts, sortBy).ToList();

                // Get total count
                var totalCount = posts.Count;

                // Apply pagination
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var postResponses = await MapPostsToResponseAsync(paginatedPosts, currentUserId);

                return new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsByUserAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                var query = await _postRepository.FindAsync(p => p.UserId == userId && p.Status == PostStatus.Active);
                var posts = query.ToList();

                // Sort by recent
                posts = posts.OrderByDescending(p => p.CreatedAt).ToList();

                // Get total count
                var totalCount = posts.Count;

                // Apply pagination
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var postResponses = await MapPostsToResponseAsync(paginatedPosts, currentUserId);

                return new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetFeedAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                // Get user's following
                var followingQuery = await _followRepository.FindAsync(f => f.FollowerId == userId);
                var followingIds = followingQuery.Select(f => f.FollowedId).ToList();

                // Include user's own posts
                followingIds.Add(userId);

                // Get posts from followed users and own posts
                var allPostsQuery = await _postRepository.FindAsync(p => followingIds.Contains(p.UserId) && p.Status == PostStatus.Active);
                var posts = allPostsQuery.ToList();

                // Sort by recent
                posts = posts.OrderByDescending(p => p.CreatedAt).ToList();

                // Get total count
                var totalCount = posts.Count;

                // Apply pagination
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var postResponses = await MapPostsToResponseAsync(paginatedPosts, userId);

                return new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting feed for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsByTagAsync(string tag, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                // Find tag by name
                var queryTag = await _tagRepository.SingleOrDefaultAsync(t => t.Name.ToLower() == tag.ToLower() || t.Slug.ToLower() == tag.ToLower());
                if (queryTag == null)
                {
                    return new PagedResponse<PostSummaryResponse>
                    {
                        Items = new List<PostSummaryResponse>(),
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                // Get post IDs with this tag
                var postTagsQuery = await _postTagRepository.FindAsync(pt => pt.TagId == queryTag.Id);
                var postIds = postTagsQuery.Select(pt => pt.PostId).ToList();

                // Get posts
                var postsQuery = await _postRepository.FindAsync(p => postIds.Contains(p.Id) && p.Status == PostStatus.Active);
                var posts = postsQuery.ToList();

                // Sort by recent
                posts = posts.OrderByDescending(p => p.CreatedAt).ToList();

                // Get total count
                var totalCount = posts.Count;

                // Apply pagination
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var postResponses = await MapPostsToResponseAsync(paginatedPosts, currentUserId);

                return new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for tag '{tag}'");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsByLocationAsync(Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var query = await _postRepository.FindAsync(p => p.LocationId == locationId && p.Status == PostStatus.Active);
                var posts = query.ToList();

                // Sort by recent
                posts = posts.OrderByDescending(p => p.CreatedAt).ToList();

                // Get total count
                var totalCount = posts.Count;

                // Apply pagination
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var postResponses = await MapPostsToResponseAsync(paginatedPosts, currentUserId);

                return new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for location ID {locationId}");
                throw;
            }
        }

        public async Task<PostDetailResponse> GetPostByIdAsync(Guid postId, Guid? currentUserId = null)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                var postResponse = await MapPostToDetailResponseAsync(post, currentUserId);
                return postResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting post with ID {postId}");
                throw;
            }
        }

        public async Task<PostDetailResponse> CreatePostAsync(Guid userId, CreatePostRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Create post
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Content = request.Content,
                    Type = PostType.Standard,
                    Status = PostStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    LocationId = request.LocationId,
                    LocationName = request.LocationName,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                };

                await _postRepository.AddAsync(post);
                await _postRepository.SaveChangesAsync();

                // Process media
                if (request.MediaIds != null && request.MediaIds.Count > 0)
                {
                    foreach (var mediaId in request.MediaIds)
                    {
                        var media = await _mediaRepository.GetByIdAsync(mediaId);
                        if (media != null)
                        {
                            // Link media to post
                            media.EntityId = post.Id;
                            media.EntityType = "Post";
                            _mediaRepository.Update(media);
                        }
                    }
                    await _mediaRepository.SaveChangesAsync();
                }

                // Process tags
                if (request.Tags != null && request.Tags.Count > 0)
                {
                    await ProcessPostTagsAsync(post.Id, request.Tags);
                }

                // Return post with details
                return await GetPostByIdAsync(post.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                throw;
            }
        }

        public async Task<PostDetailResponse> UpdatePostAsync(Guid postId, Guid userId, UpdatePostRequest request)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                // Check if user is the owner
                if (post.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this post");
                }

                // Update post
                post.Content = request.Content;
                post.LocationId = request.LocationId;
                post.LocationName = request.LocationName;
                post.Latitude = request.Latitude;
                post.Longitude = request.Longitude;
                post.LastModifiedAt = DateTime.UtcNow;

                _postRepository.Update(post);
                await _postRepository.SaveChangesAsync();

                // Update media
                // First, get current media
                var currentMediaQuery = await _mediaRepository.FindAsync(m => m.EntityId == postId && m.EntityType == "Post");
                var currentMedia = currentMediaQuery.ToList();

                // Detach removed media
                foreach (var media in currentMedia)
                {
                    if (!request.MediaIds.Contains(media.Id))
                    {
                        media.EntityId = null;
                        media.EntityType = null;
                        _mediaRepository.Update(media);
                    }
                }

                // Attach new media
                foreach (var mediaId in request.MediaIds)
                {
                    var media = await _mediaRepository.GetByIdAsync(mediaId);
                    if (media != null && (media.EntityId == null || media.EntityId != postId))
                    {
                        media.EntityId = post.Id;
                        media.EntityType = "Post";
                        _mediaRepository.Update(media);
                    }
                }
                await _mediaRepository.SaveChangesAsync();

                // Update tags
                // First, remove existing tags
                var existingTagsQuery = await _postTagRepository.FindAsync(pt => pt.PostId == postId);
                var existingTags = existingTagsQuery.ToList();

                foreach (var tag in existingTags)
                {
                    _postTagRepository.Remove(tag);
                }
                await _postTagRepository.SaveChangesAsync();

                // Add new tags
                if (request.Tags != null && request.Tags.Count > 0)
                {
                    await ProcessPostTagsAsync(post.Id, request.Tags);
                }

                // Return updated post
                return await GetPostByIdAsync(post.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating post with ID {postId}");
                throw;
            }
        }

        public async Task<bool> DeletePostAsync(Guid postId, Guid userId)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                // Check if user is the owner
                if (post.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to delete this post");
                }

                // Soft delete
                post.IsDeleted = true;
                post.DeletedAt = DateTime.UtcNow;
                post.DeletedBy = userId;
                post.Status = PostStatus.Archived;

                _postRepository.Update(post);
                await _postRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting post with ID {postId}");
                throw;
            }
        }

        public async Task<bool> LikePostAsync(Guid postId, Guid userId)
        {
            try
            {
                // Check if post exists
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                // Check if already liked
                var existingLike = await _likeRepository.SingleOrDefaultAsync(
                    l => l.EntityId == postId && l.UserId == userId && l.EntityType == "Post");

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
                    EntityId = postId,
                    EntityType = "Post",
                    CreatedAt = DateTime.UtcNow
                };

                await _likeRepository.AddAsync(like);
                await _likeRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error liking post with ID {postId}");
                throw;
            }
        }

        public async Task<bool> UnlikePostAsync(Guid postId, Guid userId)
        {
            try
            {
                // Find like
                var like = await _likeRepository.SingleOrDefaultAsync(
                    l => l.EntityId == postId && l.UserId == userId && l.EntityType == "Post");

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
                _logger.LogError(ex, $"Error unliking post with ID {postId}");
                throw;
            }
        }

        public async Task<PagedResponse<CommentResponse>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize)
        {
            try
            {
                // Check if post exists
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                // Get root comments (those without a parent)
                var commentsQuery = await _commentRepository.FindAsync(
                    c => c.EntityId == postId && c.EntityType == "Post" && c.ParentId == null && !c.IsDeleted);
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
                _logger.LogError(ex, $"Error getting comments for post ID {postId}");
                throw;
            }
        }

        public async Task<CommentResponse> AddCommentAsync(Guid postId, Guid userId, CreateCommentRequest request)
        {
            try
            {
                // Check if post exists
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
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

                    // Check if parent comment belongs to the same post
                    if (parentComment.EntityId != postId || parentComment.EntityType != "Post")
                    {
                        throw new InvalidOperationException("Parent comment does not belong to this post");
                    }
                }

                var comment = new Comment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Content = request.Content,
                    EntityId = postId,
                    EntityType = "Post",
                    ParentId = request.ParentId,
                    Status = CommentStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };

                await _commentRepository.AddAsync(comment);
                await _commentRepository.SaveChangesAsync();

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
                _logger.LogError(ex, $"Error adding comment to post ID {postId}");
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

                if (comment.UserId != userId)
                {
                    var post = await _postRepository.GetByIdAsync(comment.EntityId);
                    if (post.UserId != userId)
                    {
                        throw new UnauthorizedAccessException("You are not authorized to delete this comment");
                    }
                }

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

        public async Task<PagedResponse<PostSummaryResponse>> SearchPostsAsync(string query, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    throw new ArgumentException("Search query cannot be empty");
                }

                var postsQuery = await _postRepository.FindAsync(p =>
                    p.Status == PostStatus.Active &&
                    (p.Content.Contains(query) || p.LocationName.Contains(query)));
                var posts = postsQuery.ToList();

                var tagsQuery = await _tagRepository.FindAsync(t =>
                    t.Name.Contains(query) || t.Slug.Contains(query));
                var tagIds = tagsQuery.Select(t => t.Id).ToList();

                if (tagIds.Any())
                {
                    var postTagsQuery = await _postTagRepository.FindAsync(pt => tagIds.Contains(pt.TagId));
                    var postIds = postTagsQuery.Select(pt => pt.PostId).ToList();

                    if (postIds.Any())
                    {
                        var taggedPostsQuery = await _postRepository.FindAsync(p =>
                            postIds.Contains(p.Id) && p.Status == PostStatus.Active);
                        var taggedPosts = taggedPostsQuery.ToList();

                        posts = posts.Union(taggedPosts, new PostComparer()).ToList();
                    }
                }

                posts = posts.OrderByDescending(p => p.CreatedAt).ToList();

                var totalCount = posts.Count;

                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var postResponses = await MapPostsToResponseAsync(paginatedPosts, currentUserId);

                return new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching posts for query '{query}'");
                throw;
            }
        }

        #region Helper Methods

        private IEnumerable<Post> ApplySorting(IEnumerable<Post> posts, string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "recent" => posts.OrderByDescending(p => p.CreatedAt),
                "popular" => posts.OrderByDescending(p => p.ViewCount),
                _ => posts.OrderByDescending(p => p.CreatedAt)
            };
        }

        private async Task<List<PostSummaryResponse>> MapPostsToResponseAsync(List<Post> posts, Guid? currentUserId = null)
        {
            var postResponses = new List<PostSummaryResponse>();

            foreach (var post in posts)
            {
                var postResponse = await MapPostToSummaryResponseAsync(post, currentUserId);
                postResponses.Add(postResponse);
            }

            return postResponses;
        }

        private async Task<PostSummaryResponse> MapPostToSummaryResponseAsync(Post post, Guid? currentUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(post.UserId);

            var mediaQuery = await _mediaRepository.FindAsync(m => m.EntityId == post.Id && m.EntityType == "Post");
            var media = mediaQuery.ToList();

            var likesCount = (await _likeRepository.FindAsync(l => l.EntityId == post.Id && l.EntityType == "Post")).Count();

            var commentsCount = (await _commentRepository.FindAsync(c => c.EntityId == post.Id && c.EntityType == "Post" && !c.IsDeleted)).Count();

            var isLiked = false;
            if (currentUserId.HasValue)
            {
                isLiked = await _likeRepository.ExistsAsync(l =>
                    l.EntityId == post.Id && l.UserId == currentUserId.Value && l.EntityType == "Post");
            }

            var tagIds = (await _postTagRepository.FindAsync(pt => pt.PostId == post.Id))
                .Select(pt => pt.TagId).ToList();
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

            LocationSummaryResponse location = null;
            if (post.LocationId.HasValue)
            {
                var locationEntity = await _locationRepository.GetByIdAsync(post.LocationId.Value);
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
                else if (!string.IsNullOrEmpty(post.LocationName))
                {
                    location = new LocationSummaryResponse
                    {
                        Id = null,
                        Name = post.LocationName,
                        Latitude = post.Latitude,
                        Longitude = post.Longitude,
                        Type = null
                    };
                }
            }

            return new PostSummaryResponse
            {
                Id = post.Id,
                User = new Application.Users.DTOs.UserSummaryResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfileImageUrl = user.ProfileImageUrl
                },
                Content = post.Content,
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
                CreatedAt = post.CreatedAt,
                LikesCount = likesCount,
                CommentsCount = commentsCount,
                Tags = tags,
                Location = location,
                IsLikedByCurrentUser = isLiked
            };
        }

        private async Task<PostDetailResponse> MapPostToDetailResponseAsync(Post post, Guid? currentUserId = null)
        {
            var summary = await MapPostToSummaryResponseAsync(post, currentUserId);

            var commentsQuery = await _commentRepository.FindAsync(
                c => c.EntityId == post.Id && c.EntityType == "Post" && c.ParentId == null && !c.IsDeleted);
            var comments = commentsQuery.ToList();

            comments = comments.OrderByDescending(c => c.CreatedAt).ToList();

            var commentResponses = await MapCommentsToResponseAsync(comments);

            var likesQuery = await _likeRepository.FindAsync(l => l.EntityId == post.Id && l.EntityType == "Post");
            var likes = likesQuery.OrderByDescending(l => l.CreatedAt).Take(10).ToList();

            var likedByUsers = new List<Application.Users.DTOs.UserSummaryResponse>();
            foreach (var like in likes)
            {
                var user = await _userRepository.GetByIdAsync(like.UserId);
                if (user != null)
                {
                    likedByUsers.Add(new Application.Users.DTOs.UserSummaryResponse
                    {
                        Id = user.Id,
                        Username = user.Username,
                        ProfileImageUrl = user.ProfileImageUrl
                    });
                }
            }

            // Create detail response
            var detailResponse = new PostDetailResponse
            {
                Id = summary.Id,
                User = summary.User,
                Content = summary.Content,
                Media = summary.Media,
                CreatedAt = summary.CreatedAt,
                LikesCount = summary.LikesCount,
                CommentsCount = summary.CommentsCount,
                Tags = summary.Tags,
                Location = summary.Location,
                IsLikedByCurrentUser = summary.IsLikedByCurrentUser,
                Comments = commentResponses,
                LikedByUsers = likedByUsers
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

        private async Task ProcessPostTagsAsync(Guid postId, List<string> tagNames)
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

                // Create post-tag relationship
                var postTag = new PostTag
                {
                    PostId = postId,
                    TagId = tag.Id,
                    CreatedAt = DateTime.UtcNow
                };

                await _postTagRepository.AddAsync(postTag);
            }

            await _postTagRepository.SaveChangesAsync();
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

    public class PostComparer : IEqualityComparer<Post>
    {
        public bool Equals(Post x, Post y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(Post obj)
        {
            return obj.Id.GetHashCode();
        }
    }
    #endregion
}
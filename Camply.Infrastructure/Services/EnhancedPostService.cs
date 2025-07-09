using Camply.Application.Common.Interfaces;
using Camply.Application.Common.Models;
using Camply.Application.Media.Interfaces;
using Camply.Application.Posts.DTOs;
using Camply.Application.Posts.Interfaces;
using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace Camply.Infrastructure.Services
{
    public class EnhancedPostService : IPostService
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
        private readonly ICacheService _cacheService; 
        private readonly ILogger<EnhancedPostService> _logger;
        private readonly IMediaService _mediaService;

        private const string POST_CACHE_KEY = "post:{0}";
        private const string POSTS_CACHE_KEY = "posts:{0}:{1}:{2}:{3}:{4}"; // page:size:sort:userId:timestamp
        private const string USER_POSTS_CACHE_KEY = "user_posts:{0}:{1}:{2}:{3}"; // userId:page:size:currentUserId
        private const string FEED_CACHE_KEY = "feed:{0}:{1}:{2}:{3}"; // userId:page:size:timestamp
        private const string POST_LIKES_COUNT_KEY = "post_likes_count:{0}";
        private const string POST_COMMENTS_COUNT_KEY = "post_comments_count:{0}";
        private const string USER_LIKED_POST_KEY = "user_liked:{0}:{1}"; // userId:postId
        private const string FOLLOWING_CACHE_KEY = "following:{0}"; // userId
        private const string POST_TAGS_CACHE_KEY = "post_tags:{0}"; // postId
        private const string TAG_POSTS_CACHE_KEY = "tag_posts:{0}:{1}:{2}:{3}"; // tag:page:size:userId

        public EnhancedPostService(
            IRepository<Post> postRepository,
            IRepository<User> userRepository,
            IRepository<Media> mediaRepository,
            IRepository<Comment> commentRepository,
            IRepository<Like> likeRepository,
            IRepository<Tag> tagRepository,
            IRepository<PostTag> postTagRepository,
            IRepository<Follow> followRepository,
            IRepository<Location> locationRepository,
            ICacheService cacheService,
            ILogger<EnhancedPostService> logger,
            IMediaService mediaService)
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
            _cacheService = cacheService;
            _logger = logger;
            _mediaService = mediaService;
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsAsync(
            int pageNumber, int pageSize, string sortBy = "recent", Guid? currentUserId = null)
        {
            var cacheTimestamp = DateTime.UtcNow.ToString("yyyyMMddHH");
            var cacheKey = string.Format(POSTS_CACHE_KEY, pageNumber, pageSize, sortBy, currentUserId?.ToString() ?? "anonymous", cacheTimestamp);

            var cachedResult = await _cacheService.GetAsync<PagedResponse<PostSummaryResponse>>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Posts retrieved from cache for key: {CacheKey}", cacheKey);
                return cachedResult;
            }

            try
            {
                var query = await _postRepository.FindAsync(p => p.Status == PostStatus.Active);
                var posts = query.ToList();
                var sortedPosts = ApplySorting(posts, sortBy).ToList();

                var totalCount = sortedPosts.Count;

                var paginatedPosts = sortedPosts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var postResponses = await MapPostsToResponseWithCacheAsync(paginatedPosts, currentUserId);

                var result = new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
                _logger.LogDebug("Posts cached for key: {CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsByUserAsync(
            Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var cacheKey = string.Format(USER_POSTS_CACHE_KEY, userId, pageNumber, pageSize, currentUserId?.ToString() ?? "anonymous");

            var cachedResult = await _cacheService.GetAsync<PagedResponse<PostSummaryResponse>>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("User posts retrieved from cache for key: {CacheKey}", cacheKey);
                return cachedResult;
            }

            try
            {
                var user = await GetUserFromCacheAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                var query = await _postRepository.FindAsync(p => p.UserId == userId && p.Status == PostStatus.Active);
                var posts = query.OrderByDescending(p => p.CreatedAt).ToList();

                var totalCount = posts.Count;
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var postResponses = await MapPostsToResponseWithCacheAsync(paginatedPosts, currentUserId);

                var result = new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetFeedAsync(Guid userId, int pageNumber, int pageSize)
        {
            var cacheTimestamp = DateTime.UtcNow.ToString("yyyyMMddHH");
            var cacheKey = string.Format(FEED_CACHE_KEY, userId, pageNumber, pageSize, cacheTimestamp);

            var cachedFeed = await _cacheService.GetAsync<PagedResponse<PostSummaryResponse>>(cacheKey);
            if (cachedFeed != null)
            {
                _logger.LogDebug("Feed retrieved from cache for user: {UserId}", userId);
                return cachedFeed;
            }

            try
            {
                var followingIds = await GetFollowingIdsFromCacheAsync(userId) ?? new List<Guid>();
                followingIds.Add(userId);

                var allPosts = await _postRepository.FindAsync(p => p.Status == PostStatus.Active);
                var feedPosts = allPosts.Where(post => followingIds.Contains(post.UserId))
                                       .OrderByDescending(post => post.CreatedAt)
                                       .ToList();

                var totalCount = feedPosts.Count;

                var posts = feedPosts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var postResponses = await MapPostsToResponseWithCacheAsync(posts, userId);

                var result = new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting feed for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsByTagAsync(
            string tag, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var cacheKey = string.Format(TAG_POSTS_CACHE_KEY, tag, pageNumber, pageSize, currentUserId?.ToString() ?? "anonymous");

            var cachedResult = await _cacheService.GetAsync<PagedResponse<PostSummaryResponse>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

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
                var posts = postsQuery.OrderByDescending(p => p.CreatedAt).ToList();

                var totalCount = posts.Count;
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var postResponses = await MapPostsToResponseWithCacheAsync(paginatedPosts, currentUserId);

                var result = new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                // Cache for 20 minutes (tag-based queries are less frequent)
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(20));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for tag '{tag}'");
                throw;
            }
        }

        public async Task<PostDetailResponse> GetPostByIdAsync(Guid postId, Guid? currentUserId = null)
        {
            try
            {
                var post = await GetPostFromCacheAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                var postResponse = await MapPostToDetailResponseWithCacheAsync(post, currentUserId);
                return postResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting post with ID {postId}");
                throw;
            }
        }

        public async Task<bool> LikePostAsync(Guid postId, Guid userId)
        {
            try
            {
                // Check if post exists
                var post = await GetPostFromCacheAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                // Check if already liked using cache
                var userLikedKey = string.Format(USER_LIKED_POST_KEY, userId, postId);
                var alreadyLiked = await _cacheService.ExistsAsync(userLikedKey);

                if (alreadyLiked)
                {
                    return true; // Already liked
                }

                // Check in database as fallback
                var existingLike = await _likeRepository.SingleOrDefaultAsync(
                    l => l.EntityId == postId && l.UserId == userId && l.EntityType == "Post");

                if (existingLike != null)
                {
                    // Cache the like for future checks
                    await _cacheService.SetAsync(userLikedKey, true, TimeSpan.FromDays(30));
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

                // Update cache
                await _cacheService.SetAsync(userLikedKey, true, TimeSpan.FromDays(30));

                // Increment like counter
                var likesCountKey = string.Format(POST_LIKES_COUNT_KEY, postId);
                await _cacheService.IncrementAsync(likesCountKey);

                // Invalidate related caches
                await InvalidatePostCachesAsync(postId);

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
                    return true; // Not liked
                }

                // Remove like
                _likeRepository.Remove(like);
                await _likeRepository.SaveChangesAsync();

                // Update cache
                var userLikedKey = string.Format(USER_LIKED_POST_KEY, userId, postId);
                await _cacheService.RemoveAsync(userLikedKey);

                // Decrement like counter
                var likesCountKey = string.Format(POST_LIKES_COUNT_KEY, postId);
                await _cacheService.DecrementAsync(likesCountKey);

                // Invalidate related caches
                await InvalidatePostCachesAsync(postId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unliking post with ID {postId}");
                throw;
            }
        }

        public async Task<PostDetailResponse> CreatePostAsync(Guid userId, CreatePostRequest request)
        {
            try
            {
                var user = await GetUserFromCacheAsync(userId);
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

                // Cache the new post
                await CachePostAsync(post);

                // Process media
                if (request.MediaIds != null && request.MediaIds.Count > 0)
                {
                    foreach (var mediaId in request.MediaIds)
                    {
                        var media = await _mediaRepository.GetByIdAsync(mediaId);
                        if (media != null)
                        {
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

                // Invalidate user-related caches
                await InvalidateUserCachesAsync(userId);

                // Return post with details
                return await GetPostByIdAsync(post.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                throw;
            }
        }

        // Diğer method'lar (UpdatePostAsync, DeletePostAsync, etc.) aynı şekilde cache entegrasyonu ile...
        // Yer tasarrufu için sadana key method'ları gösteriyorum

        #region Cache Helper Methods

        private async Task<Post> GetPostFromCacheAsync(Guid postId)
        {
            var cacheKey = string.Format(POST_CACHE_KEY, postId);
            var cachedPost = await _cacheService.GetAsync<Post>(cacheKey);

            if (cachedPost != null)
            {
                return cachedPost;
            }

            var post = await _postRepository.GetByIdAsync(postId);
            if (post != null)
            {
                await _cacheService.SetAsync(cacheKey, post, TimeSpan.FromHours(2));
            }

            return post;
        }

        private async Task<User> GetUserFromCacheAsync(Guid userId)
        {
            var cacheKey = $"user:{userId}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);

            if (cachedUser != null)
            {
                return cachedUser;
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                var userDto = new
                {
                    Id = user.Id,
                    Name = user.Name,
                    Surname = user.Surname,
                    Username = user.Username,
                    Email = user.Email,
                    ProfileImageUrl = user.ProfileImageUrl,
                    Bio = user.Bio,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt
                };
                await _cacheService.SetAsync(cacheKey, userDto, TimeSpan.FromHours(4));
            }

            return user;
        }

        private async Task<List<Guid>> GetFollowingIdsFromCacheAsync(Guid userId)
        {
            var cacheKey = string.Format(FOLLOWING_CACHE_KEY, userId);
            var cachedFollowing = await _cacheService.GetAsync<List<Guid>>(cacheKey);
            if (cachedFollowing != null)
            {
                return cachedFollowing;
            }

            try
            {
                var followingQuery = await _followRepository.FindAsync(f => f.FollowerId == userId);
                var followingIds = followingQuery?.Select(f => f.FollowedId).ToList() ?? new List<Guid>();
                await _cacheService.SetAsync(cacheKey, followingIds, TimeSpan.FromHours(1));
                return followingIds;
            }
            catch (Exception ex)
            {
                return new List<Guid>(); 
            }
        }

        private async Task<int> GetLikesCountFromCacheAsync(Guid postId)
        {
            var cacheKey = string.Format(POST_LIKES_COUNT_KEY, postId);
            var cachedCount = await _cacheService.GetCounterAsync(cacheKey);

            if (cachedCount > 0)
            {
                return (int)cachedCount;
            }

            // Fallback to database
            var likesCount = (await _likeRepository.FindAsync(l => l.EntityId == postId && l.EntityType == "Post")).Count();

            // Cache the count
            await _cacheService.SetAsync(cacheKey, likesCount, TimeSpan.FromHours(1));

            return likesCount;
        }

        private async Task<int> GetCommentsCountFromCacheAsync(Guid postId)
        {
            var cacheKey = string.Format(POST_COMMENTS_COUNT_KEY, postId);
            var cachedCount = await _cacheService.GetCounterAsync(cacheKey);

            if (cachedCount > 0)
            {
                return (int)cachedCount;
            }

            // Fallback to database
            var commentsCount = (await _commentRepository.FindAsync(c => c.EntityId == postId && c.EntityType == "Post" && !c.IsDeleted)).Count();

            // Cache the count
            await _cacheService.SetAsync(cacheKey, commentsCount, TimeSpan.FromHours(1));

            return commentsCount;
        }

        private async Task<bool> IsPostLikedByUserFromCacheAsync(Guid postId, Guid userId)
        {
            var cacheKey = string.Format(USER_LIKED_POST_KEY, userId, postId);
            return await _cacheService.ExistsAsync(cacheKey);
        }

        private async Task CachePostAsync(Post post)
        {
            var cacheKey = string.Format(POST_CACHE_KEY, post.Id);
            await _cacheService.SetAsync(cacheKey, post, TimeSpan.FromHours(2));
        }

        private async Task InvalidatePostCachesAsync(Guid postId)
        {
            var postCacheKey = string.Format(POST_CACHE_KEY, postId);
            await _cacheService.RemoveAsync(postCacheKey);

            var likesCountKey = string.Format(POST_LIKES_COUNT_KEY, postId);
            var commentsCountKey = string.Format(POST_COMMENTS_COUNT_KEY, postId);
            await _cacheService.RemoveAsync(likesCountKey);
            await _cacheService.RemoveAsync(commentsCountKey);

            await _cacheService.RemovePatternAsync("posts:*");
            await _cacheService.RemovePatternAsync("feed:*");
        }

        private async Task InvalidateUserCachesAsync(Guid userId)
        {
            await _cacheService.RemovePatternAsync($"user_posts:{userId}:*");
            await _cacheService.RemovePatternAsync("feed:*");
            await _cacheService.RemovePatternAsync("posts:*");
        }

        private async Task<List<PostSummaryResponse>> MapPostsToResponseWithCacheAsync(List<Post> posts, Guid? currentUserId = null)
        {
            if (!posts.Any()) return new List<PostSummaryResponse>();

            var postIds = posts.Select(p => p.Id).ToList();
            var userIds = posts.Select(p => p.UserId).Distinct().ToList();
            var locationIds = posts.Where(p => p.LocationId.HasValue).Select(p => p.LocationId.Value).Distinct().ToList();

            var mediaTask =await _mediaRepository.FindAsync(m => m.EntityId.HasValue && postIds.Contains(m.EntityId.Value) && m.EntityType == "Post");
            var postTagsTask = await _postTagRepository.FindAsync(pt => postIds.Contains(pt.PostId));
            var locationsResult = locationIds.Count > 0
                         ? await _locationRepository.FindAsync(l => locationIds.Contains(l.Id))
                         : Enumerable.Empty<Location>();


            var mediaLookup = ( mediaTask)
                .ToList()
                .GroupBy(m => m.EntityId.GetValueOrDefault()) 
                .ToDictionary(g => g.Key, g => g.ToList());
            var postTagsLookup = ( postTagsTask).ToList().GroupBy(pt => pt.PostId).ToDictionary(g => g.Key, g => g.Select(pt => pt.TagId).ToList());
            var locationsLookup = locationIds.Any() ? (locationsResult).ToList().ToDictionary(l => l.Id, l => l) : new Dictionary<Guid, Location>();

            var allTagIds = postTagsLookup.Values.SelectMany(tagIds => tagIds).Distinct().ToList();
            var tagsLookup = allTagIds.Any() ? 
                (await _tagRepository.FindAsync(t => allTagIds.Contains(t.Id))).ToList().ToDictionary(t => t.Id, t => t) : 
                new Dictionary<Guid, Tag>();

            var postResponses = new List<PostSummaryResponse>();

            foreach (var post in posts)
            {
                var postResponse = await MapPostToSummaryResponseWithBulkDataAsync(post, currentUserId, mediaLookup, postTagsLookup, locationsLookup, tagsLookup);
                postResponses.Add(postResponse);
            }

            return postResponses;
        }

        private async Task<PostSummaryResponse> MapPostToSummaryResponseWithBulkDataAsync(
            Post post,
            Guid? currentUserId,
            Dictionary<Guid, List<Media>> mediaLookup,
            Dictionary<Guid, List<Guid>> postTagsLookup,
            Dictionary<Guid, Location> locationsLookup,
            Dictionary<Guid, Tag> tagsLookup)
        {
            var user = await GetUserFromCacheAsync(post.UserId);

            // Execute count operations in parallel
            var likesCountTask =await GetLikesCountFromCacheAsync(post.Id);
            var commentsCountTask = await GetCommentsCountFromCacheAsync(post.Id);


            var likesCount =  likesCountTask;
            var commentsCount =  commentsCountTask;

            var isLiked = false;
            if (currentUserId.HasValue)
            {
                isLiked = await IsPostLikedByUserFromCacheAsync(post.Id, currentUserId.Value);
            }

            // Get media from lookup
            var media = mediaLookup.TryGetValue(post.Id, out var postMedia) ? postMedia : new List<Media>();

            // Get tags from lookup
            var tags = new List<TagResponse>();
            if (postTagsLookup.TryGetValue(post.Id, out var tagIds) && tagIds.Any())
            {
                tags = tagIds.Where(tagId => tagsLookup.ContainsKey(tagId))
                            .Select(tagId => tagsLookup[tagId])
                            .Select(t => new TagResponse
                            {
                                Id = t.Id,
                                Name = t.Name,
                                Slug = t.Slug,
                                UsageCount = t.UsageCount
                            }).ToList();
            }

            // Get location from lookup
            BlogLocationSummaryResponse location = null;
            if (post.LocationId.HasValue && locationsLookup.TryGetValue(post.LocationId.Value, out var locationEntity))
            {
                location = new BlogLocationSummaryResponse
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
                location = new BlogLocationSummaryResponse
                {
                    Id = null,
                    Name = post.LocationName,
                    Latitude = post.Latitude,
                    Longitude = post.Longitude,
                    Type = null
                };
            }

            return new PostSummaryResponse
            {
                Id = post.Id,
                User = new Application.Users.DTOs.UserSummaryResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfileImageUrl = await _mediaService.GenerateSecureUrlAsync(user.ProfileImageUrl)
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

        private async Task<PostSummaryResponse> MapPostToSummaryResponseWithCacheAsync(Post post, Guid? currentUserId = null)
        {
            var user = await GetUserFromCacheAsync(post.UserId);
            
            var likesCountTask =await GetLikesCountFromCacheAsync(post.Id);
            var commentsCountTask =await GetCommentsCountFromCacheAsync(post.Id);
            var mediaTask =await _mediaRepository.FindAsync(m => m.EntityId == post.Id && m.EntityType == "Post");
            var postTagsTask =await _postTagRepository.FindAsync(pt => pt.PostId == post.Id);
            
            
            var likesCount =  likesCountTask;
            var commentsCount =  commentsCountTask;
            var mediaQuery =  mediaTask;
            var media = mediaQuery.ToList();
            
            var isLiked = false;
            if (currentUserId.HasValue)
            {
                isLiked = await IsPostLikedByUserFromCacheAsync(post.Id, currentUserId.Value);
            }

            var tagIds = ( postTagsTask).Select(pt => pt.TagId).ToList();
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

            BlogLocationSummaryResponse location = null;
            if (post.LocationId.HasValue)
            {
                var locationEntity = await _locationRepository.GetByIdAsync(post.LocationId.Value);
                if (locationEntity != null)
                {
                    location = new BlogLocationSummaryResponse
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
                    location = new BlogLocationSummaryResponse
                    {
                        Id = null,
                        Name = post.LocationName,
                        Latitude = post.Latitude,
                        Longitude = post.Longitude,
                        Type = null
                    };
                }
            }
            else if (!string.IsNullOrEmpty(post.LocationName))
            {
                location = new BlogLocationSummaryResponse
                {
                    Id = null,
                    Name = post.LocationName,
                    Latitude = post.Latitude,
                    Longitude = post.Longitude,
                    Type = null
                };
            }

            return new PostSummaryResponse
            {
                Id = post.Id,
                User = new Application.Users.DTOs.UserSummaryResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfileImageUrl = await _mediaService.GenerateSecureUrlAsync(user.ProfileImageUrl)
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

        private async Task<PostDetailResponse> MapPostToDetailResponseWithCacheAsync(Post post, Guid? currentUserId = null)
        {
            var summary = await MapPostToSummaryResponseWithCacheAsync(post, currentUserId);

            // Comments mapping remains the same for now (can be optimized later)
            var commentsQuery = await _commentRepository.FindAsync(
                c => c.EntityId == post.Id && c.EntityType == "Post" && c.ParentId == null && !c.IsDeleted);
            var comments = commentsQuery.OrderByDescending(c => c.CreatedAt).ToList();
            var commentResponses = await MapCommentsToResponseAsync(comments);

            // Liked by users (top 10)
            var likesQuery = await _likeRepository.FindAsync(l => l.EntityId == post.Id && l.EntityType == "Post");
            var likes = likesQuery.OrderByDescending(l => l.CreatedAt).Take(10).ToList();

            var likedByUsers = new List<Application.Users.DTOs.UserSummaryResponse>();
            foreach (var like in likes)
            {
                var user = await GetUserFromCacheAsync(like.UserId);
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

            return new PostDetailResponse
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
        }

        #endregion
        #region Original Helper Methods (Cache entegrasyonsuz)

        private IEnumerable<Post> ApplySorting(IEnumerable<Post> posts, string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "recent" => posts.OrderByDescending(p => p.CreatedAt),
                "popular" => posts.OrderByDescending(p => p.ViewCount),
                _ => posts.OrderByDescending(p => p.CreatedAt)
            };
        }

        private IQueryable<Post> ApplySortingToQuery(IQueryable<Post> query, string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "recent" => query.OrderByDescending(p => p.CreatedAt),
                "popular" => query.OrderByDescending(p => p.ViewCount),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };
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

                var normalizedTagName = tagName.Trim().ToLower();
                if (string.IsNullOrWhiteSpace(normalizedTagName))
                    continue;

                var slug = CreateSlug(normalizedTagName);

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
                    tag.UsageCount++;
                    _tagRepository.Update(tag);
                    await _tagRepository.SaveChangesAsync();
                }

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
            string slug = text.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug;
        }

        #endregion

        public async Task<PagedResponse<PostSummaryResponse>> GetPostsByLocationAsync(
            Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var cacheKey = $"location_posts:{locationId}:{pageNumber}:{pageSize}:{currentUserId?.ToString() ?? "anonymous"}";

            var cachedResult = await _cacheService.GetAsync<PagedResponse<PostSummaryResponse>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var query = await _postRepository.FindAsync(p => p.LocationId == locationId && p.Status == PostStatus.Active);
                var posts = query.OrderByDescending(p => p.CreatedAt).ToList();

                var totalCount = posts.Count;
                var paginatedPosts = posts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var postResponses = await MapPostsToResponseWithCacheAsync(paginatedPosts, currentUserId);

                var result = new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting posts for location ID {locationId}");
                throw;
            }
        }

        public async Task<PostDetailResponse> UpdatePostAsync(Guid postId, Guid userId, UpdatePostRequest request)
        {
            try
            {
                var post = await GetPostFromCacheAsync(postId);
                if (post == null)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

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

                // Update post cache
                await CachePostAsync(post);

                // Update media relationships
                var currentMediaQuery = await _mediaRepository.FindAsync(m => m.EntityId == postId && m.EntityType == "Post");
                var currentMedia = currentMediaQuery.ToList();

                foreach (var media in currentMedia)
                {
                    if (!request.MediaIds.Contains(media.Id))
                    {
                        media.EntityId = null;
                        media.EntityType = null;
                        _mediaRepository.Update(media);
                    }
                }

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
                var existingTagsQuery = await _postTagRepository.FindAsync(pt => pt.PostId == postId);
                var existingTags = existingTagsQuery.ToList();

                foreach (var tag in existingTags)
                {
                    _postTagRepository.Remove(tag);
                }
                await _postTagRepository.SaveChangesAsync();

                if (request.Tags != null && request.Tags.Count > 0)
                {
                    await ProcessPostTagsAsync(post.Id, request.Tags);
                }

                // Invalidate caches
                await InvalidatePostCachesAsync(postId);
                await InvalidateUserCachesAsync(userId);

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
                var post = await GetPostFromCacheAsync(postId);
                if (post == null)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

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

                // Invalidate all related caches
                await InvalidatePostCachesAsync(postId);
                await InvalidateUserCachesAsync(userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting post with ID {postId}");
                throw;
            }
        }

        public async Task<PagedResponse<CommentResponse>> GetCommentsAsync(Guid postId, int pageNumber, int pageSize)
        {
            var cacheKey = $"post_comments:{postId}:{pageNumber}:{pageSize}";

            var cachedResult = await _cacheService.GetAsync<PagedResponse<CommentResponse>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                var post = await GetPostFromCacheAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                var commentsQuery = await _commentRepository.FindAsync(
                    c => c.EntityId == postId && c.EntityType == "Post" && c.ParentId == null && !c.IsDeleted);
                var comments = commentsQuery.OrderByDescending(c => c.CreatedAt).ToList();

                var totalCount = comments.Count;
                var paginatedComments = comments
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var commentResponses = await MapCommentsToResponseAsync(paginatedComments);

                var result = new PagedResponse<CommentResponse>
                {
                    Items = commentResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                // Cache comments for 10 minutes
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
                return result;
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
                var post = await GetPostFromCacheAsync(postId);
                if (post == null || post.Status != PostStatus.Active)
                {
                    throw new KeyNotFoundException($"Post with ID {postId} not found");
                }

                var user = await GetUserFromCacheAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                if (request.ParentId.HasValue)
                {
                    var parentComment = await _commentRepository.GetByIdAsync(request.ParentId.Value);
                    if (parentComment == null || parentComment.IsDeleted)
                    {
                        throw new KeyNotFoundException($"Parent comment with ID {request.ParentId.Value} not found");
                    }

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

                // Increment comment counter in cache
                var commentsCountKey = string.Format(POST_COMMENTS_COUNT_KEY, postId);
                await _cacheService.IncrementAsync(commentsCountKey);

                // Invalidate comments cache
                await _cacheService.RemovePatternAsync($"post_comments:{postId}:*");
                await InvalidatePostCachesAsync(postId);

                return new CommentResponse
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
                    var post = await GetPostFromCacheAsync(comment.EntityId);
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

                // Decrement comment counter in cache
                var commentsCountKey = string.Format(POST_COMMENTS_COUNT_KEY, comment.EntityId);
                await _cacheService.DecrementAsync(commentsCountKey);

                // Invalidate comments cache
                await _cacheService.RemovePatternAsync($"post_comments:{comment.EntityId}:*");
                await InvalidatePostCachesAsync(comment.EntityId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting comment with ID {commentId}");
                throw;
            }
        }

        public async Task<PagedResponse<PostSummaryResponse>> SearchPostsAsync(
            string query, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var cacheKey = $"search_posts:{query}:{pageNumber}:{pageSize}:{currentUserId?.ToString() ?? "anonymous"}";

            var cachedResult = await _cacheService.GetAsync<PagedResponse<PostSummaryResponse>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

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

                var postResponses = await MapPostsToResponseWithCacheAsync(paginatedPosts, currentUserId);

                var result = new PagedResponse<PostSummaryResponse>
                {
                    Items = postResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                // Cache search results for 15 minutes
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching posts for query '{query}'");
                throw;
            }
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
}
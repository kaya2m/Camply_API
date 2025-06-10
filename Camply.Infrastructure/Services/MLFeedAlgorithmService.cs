using Camply.Application.MachineLearning.Interfaces;
using Camply.Application.Common.Models;
using Camply.Domain.Analytics;
using Camply.Domain.Repositories;
using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Camply.Infrastructure.Services
{
 public class MLFeedAlgorithmService : IMLFeedAlgorithmService
    {
        private readonly IRepository<Post> _postRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IMLAnalyticsRepository _analyticsRepository;
        private readonly IMLUserFeatureRepository _userFeatureRepository;
        private readonly IMLContentFeatureRepository _contentFeatureRepository;
        private readonly IMLModelService _modelService;
        private readonly IMLFeatureExtractionService _featureService;
        private readonly ICacheService _cacheService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MLFeedAlgorithmService> _logger;

        public MLFeedAlgorithmService(
            IRepository<Post> postRepository,
            IRepository<User> userRepository,
            IMLAnalyticsRepository analyticsRepository,
            IMLUserFeatureRepository userFeatureRepository,
            IMLContentFeatureRepository contentFeatureRepository,
            IMLModelService modelService,
            IMLFeatureExtractionService featureService,
            ICacheService cacheService,
            IMemoryCache memoryCache,
            ILogger<MLFeedAlgorithmService> logger)
        {
            _postRepository = postRepository;
            _userRepository = userRepository;
            _analyticsRepository = analyticsRepository;
            _userFeatureRepository = userFeatureRepository;
            _contentFeatureRepository = contentFeatureRepository;
            _modelService = modelService;
            _featureService = featureService;
            _cacheService = cacheService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<PagedResponse<PostSummaryResponseML>> GeneratePersonalizedFeedAsync(Guid userId, int page, int pageSize)
        {
            try
            {
                var cacheKey = $"feed:user:{userId}:page:{page}:size:{pageSize}";
                
                var cachedFeed = await _cacheService.GetAsync<PagedResponse<PostSummaryResponseML>>(cacheKey);
                if (cachedFeed != null)
                {
                    return cachedFeed;
                }

                var userFeatures = await _featureService.ExtractUserFeaturesAsync(userId);
                
                var candidatePosts = await GetCandidatePostsAsync(userId);
                
                var scoredPosts = new List<(PostSummaryResponseML post, double score)>();
                
                foreach (var post in candidatePosts)
                {
                    var contentFeatures = await _featureService.ExtractContentFeaturesAsync(post.Id, "Post");
                    var engagementScore = await _modelService.PredictEngagementAsync(userFeatures, contentFeatures);
                    
                    var finalScore = CalculateFinalScore(post, engagementScore, userId);
                    
                    scoredPosts.Add((post, finalScore));
                }

                var rankedPosts = scoredPosts
                    .OrderByDescending(x => x.score)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => 
                    {
                        x.post.PersonalizationScore = x.score;
                        return x.post;
                    })
                    .ToList();

                var result = new PagedResponse<PostSummaryResponseML>
                {
                    Items = rankedPosts,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalCount = scoredPosts.Count,
                    TotalPages = (int)Math.Ceiling(scoredPosts.Count / (double)pageSize)
                };

                // Cache for 15 minutes
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating personalized feed for user {UserId}", userId);
                
                return await GetFallbackFeedAsync(userId, page, pageSize);
            }
        }

        public async Task<List<PostSummaryResponseML>> GetTrendingPostsAsync(int count = 20)
        {
            var cacheKey = $"trending:posts:{count}";
            
            var cached = await _cacheService.GetAsync<List<PostSummaryResponseML>>(cacheKey);
            if (cached != null) return cached;

            // Get posts from last 24 hours with high engagement
            var since = DateTime.UtcNow.AddHours(-24);
            var posts = await _postRepository.FindAsync(p => 
                p.CreatedAt >= since && 
                p.Status == PostStatus.Active);

            var trendingPosts = posts
                .Select(p => MapToPostSummary(p))
                .OrderByDescending(p => p.EngagementScore)
                .Take(count)
                .ToList();

            await _cacheService.SetAsync(cacheKey, trendingPosts, TimeSpan.FromMinutes(30));
            return trendingPosts;
        }

        public async Task<List<PostSummaryResponseML>> GetSimilarPostsAsync(Guid postId, int count = 10)
        {
            var similarPosts = new List<PostSummaryResponseML>();
            
            try
            {
                var targetPost = await _postRepository.GetByIdAsync(postId);
                if (targetPost == null) return similarPosts;

                var targetFeatures = await _featureService.ExtractContentFeaturesAsync(postId, "Post");
                
                var recentPosts = await _postRepository.FindAsync(p => 
                    p.Id != postId && 
                    p.Status == PostStatus.Active &&
                    p.CreatedAt >= DateTime.UtcNow.AddDays(-30));

                var similarities = new List<(PostSummaryResponseML post, double similarity)>();

                foreach (var post in recentPosts.Take(500)) // Limit for performance
                {
                    var postFeatures = await _featureService.ExtractContentFeaturesAsync(post.Id, "Post");
                    var similarity = await _featureService.CalculateContentSimilarityAsync(postId, post.Id);
                    
                    if (similarity.ContainsKey("overall") && similarity["overall"] > 0.3)
                    {
                        similarities.Add((MapToPostSummary(post), similarity["overall"]));
                    }
                }

                similarPosts = similarities
                    .OrderByDescending(x => x.similarity)
                    .Take(count)
                    .Select(x => x.post)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar posts for {PostId}", postId);
            }

            return similarPosts;
        }

        public async Task TrackUserInteractionAsync(Guid userId, Guid postId, string interactionType, double duration = 0)
        {
            var interaction = new UserInteractionDocument
            {
                UserId = userId,
                ContentId = postId,
                ContentType = "Post",
                InteractionType = interactionType,
                ViewDuration = (int?)duration,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { 
                    { "Source", "Feed" }, 
                    { "Platform", "Mobile" } 
                }
            };

            await _analyticsRepository.SaveUserInteractionAsync(interaction);

            await InvalidateUserFeedCacheAsync(userId);
        }

        public async Task RefreshUserFeedCacheAsync(Guid userId)
        {
            await InvalidateUserFeedCacheAsync(userId);
            
            await _featureService.UpdateUserInterestProfileAsync(userId);
        }

        public async Task<double> PredictEngagementScoreAsync(Guid userId, Guid postId)
        {
            try
            {
                var userFeatures = await _featureService.ExtractUserFeaturesAsync(userId);
                var contentFeatures = await _featureService.ExtractContentFeaturesAsync(postId, "Post");
                
                return await _modelService.PredictEngagementAsync(userFeatures, contentFeatures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting engagement for user {UserId} and post {PostId}", userId, postId);
                return 0.5; 
            }
        }

        private async Task<List<PostSummaryResponseML>> GetCandidatePostsAsync(Guid userId)
        {
            var followedUserIds = await GetFollowedUserIdsAsync(userId);
            var recentPosts = await _postRepository.FindAsync(p => 
                (followedUserIds.Contains(p.UserId) || p.CreatedAt >= DateTime.UtcNow.AddHours(-6)) &&
                p.Status == PostStatus.Active);

            return recentPosts
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(MapToPostSummary)
                .ToList();
        }

        private async Task<List<Guid>> GetFollowedUserIdsAsync(Guid userId)
        {
            var cacheKey = $"user:following:{userId}";
            var cached = await _cacheService.GetAsync<List<Guid>>(cacheKey);
            if (cached != null) return cached;

            var user = await _userRepository.GetByIdAsync(userId);
            var followedIds = user?.Following?.Select(f => f.FollowedId).ToList() ?? new List<Guid>();

            await _cacheService.SetAsync(cacheKey, followedIds, TimeSpan.FromHours(1));
            return followedIds;
        }

        private double CalculateFinalScore(PostSummaryResponseML post, double engagementScore, Guid userId)
        {
            var timeDecay = CalculateTimeDecay(post.CreatedAt);
            var engagementBoost = Math.Log(1 + post.LikeCount + post.CommentCount * 2);
            var diversityFactor = 1.0;
            
            return engagementScore * timeDecay * (1 + engagementBoost * 0.1) * diversityFactor;
        }

        private double CalculateTimeDecay(DateTime createdAt)
        {
            var hoursSincePost = (DateTime.UtcNow - createdAt).TotalHours;
            return Math.Exp(-hoursSincePost / 24.0); 
        }

        private double CalculateEngagementScore(Post post)
        {
            var likes = post.Likes?.Count ?? 0;
            var comments = post.Comments?.Count ?? 0;
            var hoursSincePost = (DateTime.UtcNow - post.CreatedAt).TotalHours + 1;
            
            return (likes + comments * 2) / hoursSincePost;
        }

        private PostSummaryResponseML MapToPostSummary(Post post)
        {
            return new  PostSummaryResponseML
            {
                Id = post.Id,
                UserId = post.UserId,
                Username = post.User?.Username,
                UserProfileImage = post.User?.ProfileImageUrl,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                LikeCount = post.Likes?.Count ?? 0,
                CommentCount = post.Comments?.Count ?? 0,
                // Media = post.Media?.Select(m => MapToMediaSummary(m)).ToList() ?? new List<MediaSummaryResponse>(),
                // Tags = post.PostTags?.Select(pt => MapToTagResponse(pt.Tag)).ToList() ?? new List<TagResponse>(),
                EngagementScore = CalculateEngagementScore(post)
            };
        }

        private async Task<PagedResponse<PostSummaryResponseML>> GetFallbackFeedAsync(Guid userId, int page, int pageSize)
        {
            var posts = await _postRepository.FindAsync(p => p.Status == PostStatus.Active);
            var sortedPosts = posts
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToPostSummary)
                .ToList();

            return new PagedResponse<PostSummaryResponseML>
            {
                Items = sortedPosts,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = posts.Count(),
                TotalPages = (int)Math.Ceiling(posts.Count() / (double)pageSize)
            };
        }

        private async Task InvalidateUserFeedCacheAsync(Guid userId)
        {
            var pattern = $"feed:user:{userId}:*";
            await _cacheService.RemovePatternAsync(pattern);
        }
    }
}

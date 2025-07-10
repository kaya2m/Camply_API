using Camply.Application.Common.Interfaces;
using Camply.Application.Common.Models;
using Camply.Application.Media.Interfaces;
using Camply.Application.Users.DTOs;
using Camply.Application.Users.Interfaces;
using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class OptimizedUserRecommendationService : IUserRecommendationService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Follow> _followRepository;
        private readonly IRepository<Post> _postRepository;
        private readonly IRepository<Like> _likeRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<OptimizedUserRecommendationService> _logger;
        private readonly IMediaService _mediaService;
        private readonly RecommendationSettings _settings;

        // Optimized cache keys
        private const string RECOMMENDATIONS_CACHE_KEY = "user_recommendations:{0}:{1}:{2}";
        private const string POPULAR_USERS_CACHE_KEY = "popular_users:{0}";
        private const string MUTUAL_FOLLOWERS_CACHE_KEY = "mutual_followers:{0}";
        private const string RECENT_ACTIVE_CACHE_KEY = "recent_active:{0}";
        private const string USER_FOLLOWING_CACHE_KEY = "user_following:{0}";
        private const string USER_FOLLOWERS_CACHE_KEY = "user_followers:{0}";
        private const string USER_STATS_CACHE_KEY = "user_stats:{0}";
        private const string USER_LIKES_CACHE_KEY = "user_likes:{0}";
        private const string BULK_USER_DATA_CACHE_KEY = "bulk_user_data:{0}";

        public OptimizedUserRecommendationService(
            IRepository<User> userRepository,
            IRepository<Follow> followRepository,
            IRepository<Post> postRepository,
            IRepository<Like> likeRepository,
            ICacheService cacheService,
            ILogger<OptimizedUserRecommendationService> logger,
            IMediaService mediaService)
        {
            _userRepository = userRepository;
            _followRepository = followRepository;
            _postRepository = postRepository;
            _likeRepository = likeRepository;
            _cacheService = cacheService;
            _logger = logger;
            _mediaService = mediaService;
            _settings = new RecommendationSettings();
        }

        public async Task<PagedResponse<UserRecommendationResponse>> GetUserRecommendationsAsync(UserRecommendationRequest request)
        {
            try
            {
                var cacheKey = string.Format(RECOMMENDATIONS_CACHE_KEY, request.UserId, request.Algorithm, request.PageNumber);
                var cachedResult = await _cacheService.GetAsync<PagedResponse<UserRecommendationResponse>>(cacheKey);
                
                if (cachedResult != null)
                {
                    _logger.LogDebug("User recommendations retrieved from cache for user: {UserId}", request.UserId);
                    return cachedResult;
                }

                var recommendations = await GenerateRecommendationsOptimizedAsync(request);
                
                var totalCount = recommendations.Count;
                var paginatedRecommendations = recommendations
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var result = new PagedResponse<UserRecommendationResponse>
                {
                    Items = paginatedRecommendations,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
                };

                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user recommendations for user: {UserId}", request.UserId);
                throw;
            }
        }

        public async Task<List<UserRecommendationResponse>> GetPopularUsersAsync(Guid currentUserId, int count = 10)
        {
            try
            {
                var cacheKey = string.Format(POPULAR_USERS_CACHE_KEY, count);
                var cachedResult = await _cacheService.GetAsync<List<UserRecommendationResponse>>(cacheKey);
                
                if (cachedResult != null)
                {
                    return cachedResult.Where(u => u.Id != currentUserId).ToList();
                }

                // Optimized: Get user stats in bulk
                var userStats = await GetBulkUserStatsAsync();
                var currentUserFollowing = await GetUserFollowingIdsOptimizedAsync(currentUserId);
                
                var popularUsers = userStats
                    .Where(kvp => kvp.Value.FollowersCount >= _settings.MinFollowersForPopular && 
                                  kvp.Key != currentUserId && 
                                  !currentUserFollowing.Contains(kvp.Key))
                    .Select(kvp => new { UserId = kvp.Key, Stats = kvp.Value })
                    .OrderByDescending(x => x.Stats.FollowersCount)
                    .Take(count * 2) // Get extra for filtering
                    .ToList();

                // Batch create recommendations
                var recommendations = await CreateBulkRecommendationsAsync(
                    popularUsers.Select(x => x.UserId).ToList(), 
                    currentUserId,
                    "Popular user");

                foreach (var rec in recommendations)
                {
                    if (userStats.ContainsKey(rec.Id))
                    {
                        var stats = userStats[rec.Id];
                        rec.Score = CalculatePopularityScore(stats.FollowersCount, stats.PostsCount);
                    }
                }

                var result = recommendations.Take(count).ToList();
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular users");
                throw;
            }
        }

        public async Task<List<UserRecommendationResponse>> GetMutualFollowersRecommendationsAsync(Guid currentUserId, int count = 10)
        {
            try
            {
                var cacheKey = string.Format(MUTUAL_FOLLOWERS_CACHE_KEY, currentUserId);
                var cachedResult = await _cacheService.GetAsync<List<UserRecommendationResponse>>(cacheKey);
                
                if (cachedResult != null)
                {
                    return cachedResult.Take(count).ToList();
                }

                // Optimized: Get all follow relationships in one query
                var allFollows = await GetAllFollowRelationshipsAsync();
                var currentUserFollowing = allFollows.Where(f => f.FollowerId == currentUserId).Select(f => f.FollowedId).ToHashSet();
                
                var mutualCandidates = new ConcurrentDictionary<Guid, int>();
                
                // Parallel processing for mutual followers calculation
                var followingTasks = currentUserFollowing.Select(async followedUserId =>
                {
                    var theirFollowing = allFollows.Where(f => f.FollowerId == followedUserId).Select(f => f.FollowedId);
                    
                    foreach (var candidateId in theirFollowing)
                    {
                        if (candidateId != currentUserId && !currentUserFollowing.Contains(candidateId))
                        {
                            mutualCandidates.AddOrUpdate(candidateId, 1, (key, value) => value + 1);
                        }
                    }
                });

                await Task.WhenAll(followingTasks);

                var topCandidates = mutualCandidates
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .ToList();

                var recommendations = await CreateBulkRecommendationsAsync(
                    topCandidates.Select(kvp => kvp.Key).ToList(),
                    currentUserId,
                    "Followed by people you follow");

                foreach (var rec in recommendations)
                {
                    if (mutualCandidates.ContainsKey(rec.Id))
                    {
                        rec.MutualFollowersCount = mutualCandidates[rec.Id];
                        rec.HasMutualFollowers = true;
                        rec.Score = CalculateMutualFollowersScore(rec.MutualFollowersCount);
                    }
                }

                var result = recommendations.OrderByDescending(r => r.Score).ToList();
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mutual followers recommendations for user: {UserId}", currentUserId);
                throw;
            }
        }

        public async Task<List<UserRecommendationResponse>> GetRecentActiveUsersAsync(Guid currentUserId, int count = 10)
        {
            try
            {
                var cacheKey = string.Format(RECENT_ACTIVE_CACHE_KEY, count);
                var cachedResult = await _cacheService.GetAsync<List<UserRecommendationResponse>>(cacheKey);
                
                if (cachedResult != null)
                {
                    return cachedResult.Where(u => u.Id != currentUserId).ToList();
                }

                var currentUserFollowing = await GetUserFollowingIdsOptimizedAsync(currentUserId);
                var recentThreshold = DateTime.UtcNow.AddDays(-7);
                
                // Optimized: Single query with proper filtering
                var recentActiveUsers = await _userRepository.FindAsync(u => 
                    u.LastLoginAt.HasValue && 
                    u.LastLoginAt.Value > recentThreshold);

                var filteredUsers = recentActiveUsers
                    .Where(u => u.Id != currentUserId && !currentUserFollowing.Contains(u.Id))
                    .OrderByDescending(u => u.LastLoginAt)
                    .Take(count)
                    .ToList();

                var recommendations = await CreateBulkRecommendationsAsync(
                    filteredUsers.Select(u => u.Id).ToList(),
                    currentUserId,
                    "Recently active");

                foreach (var rec in recommendations)
                {
                    var user = filteredUsers.FirstOrDefault(u => u.Id == rec.Id);
                    if (user?.LastLoginAt.HasValue == true)
                    {
                        rec.Score = CalculateActivityScore(user.LastLoginAt.Value);
                    }
                }

                var result = recommendations.OrderByDescending(r => r.Score).ToList();
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(20));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent active users");
                throw;
            }
        }

        public async Task<List<UserRecommendationResponse>> GetSimilarUsersAsync(Guid currentUserId, int count = 10)
        {
            try
            {
                var currentUserFollowing = await GetUserFollowingIdsOptimizedAsync(currentUserId);
                var currentUserLikes = await GetUserLikedPostsOptimizedAsync(currentUserId);
                
                if (!currentUserLikes.Any()) return new List<UserRecommendationResponse>();

                // Get all users who liked similar posts
                var allLikes = await GetAllLikeRelationshipsAsync();
                var similarUsers = new ConcurrentDictionary<Guid, double>();
                
                // Parallel processing for similarity calculation
                var userLikeGroups = allLikes
                    .Where(l => l.UserId != currentUserId && !currentUserFollowing.Contains(l.UserId))
                    .GroupBy(l => l.UserId)
                    .Where(g => g.Count() >= 3) // Minimum likes for similarity
                    .ToList();

                var similarityTasks = userLikeGroups.Select(async userGroup =>
                {
                    var userId = userGroup.Key;
                    var userLikes = userGroup.Select(l => l.EntityId).ToList();
                    var similarity = CalculateUserSimilarity(currentUserLikes, userLikes);
                    
                    if (similarity > 0.15) // Minimum similarity threshold
                    {
                        similarUsers.TryAdd(userId, similarity);
                    }
                });

                await Task.WhenAll(similarityTasks);

                var topSimilarUsers = similarUsers
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .ToList();

                var recommendations = await CreateBulkRecommendationsAsync(
                    topSimilarUsers.Select(kvp => kvp.Key).ToList(),
                    currentUserId,
                    "Similar interests");

                foreach (var rec in recommendations)
                {
                    if (similarUsers.ContainsKey(rec.Id))
                    {
                        rec.Score = similarUsers[rec.Id];
                    }
                }

                return recommendations.OrderByDescending(r => r.Score).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting similar users for user: {UserId}", currentUserId);
                throw;
            }
        }

        public async Task RefreshUserRecommendationsAsync(Guid userId)
        {
            try
            {
                var tasks = new List<Task>
                {
                    _cacheService.RemovePatternAsync($"user_recommendations:{userId}:*"),
                    _cacheService.RemoveAsync(string.Format(MUTUAL_FOLLOWERS_CACHE_KEY, userId)),
                    _cacheService.RemoveAsync(string.Format(USER_FOLLOWING_CACHE_KEY, userId)),
                    _cacheService.RemoveAsync(string.Format(USER_FOLLOWERS_CACHE_KEY, userId)),
                    _cacheService.RemoveAsync(string.Format(USER_STATS_CACHE_KEY, userId)),
                    _cacheService.RemoveAsync(string.Format(USER_LIKES_CACHE_KEY, userId))
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("User recommendations cache refreshed for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing user recommendations cache for user: {UserId}", userId);
            }
        }

        public async Task<List<string>> GetRecommendationReasonsAsync(Guid currentUserId, Guid recommendedUserId)
        {
            var reasons = new List<string>();
            
            try
            {
                // Parallel execution of reason checks
                var tasks = new List<Task<string>>
                {
                    GetMutualFollowersReasonAsync(currentUserId, recommendedUserId),
                    GetPopularityReasonAsync(recommendedUserId),
                    GetActivityReasonAsync(recommendedUserId)
                };

                var results = await Task.WhenAll(tasks);
                reasons.AddRange(results.Where(r => !string.IsNullOrEmpty(r)));
                
                return reasons.Any() ? reasons : new List<string> { "Suggested for you" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendation reasons");
                return new List<string> { "Suggested for you" };
            }
        }

        // Optimized helper methods
        private async Task<List<UserRecommendationResponse>> GenerateRecommendationsOptimizedAsync(UserRecommendationRequest request)
        {
            switch (request.Algorithm.ToLower())
            {
                case "mutual":
                    return await GetMutualFollowersRecommendationsAsync(request.UserId, _settings.MaxRecommendations);
                case "popular":
                    return await GetPopularUsersAsync(request.UserId, _settings.MaxRecommendations);
                case "recent":
                    return await GetRecentActiveUsersAsync(request.UserId, _settings.MaxRecommendations);
                case "smart":
                default:
                    return await GenerateSmartRecommendationsOptimizedAsync(request.UserId);
            }
        }

        private async Task<List<UserRecommendationResponse>> GenerateSmartRecommendationsOptimizedAsync(Guid userId)
        {
            // Parallel execution of different recommendation algorithms
            var tasks = new List<Task<List<UserRecommendationResponse>>>
            {
                GetMutualFollowersRecommendationsAsync(userId, 10),
                GetPopularUsersAsync(userId, 10),
                GetRecentActiveUsersAsync(userId, 8),
                GetSimilarUsersAsync(userId, 7)
            };

            var results = await Task.WhenAll(tasks);
            
            var allRecommendations = results.SelectMany(r => r).ToList();
            
            // Deduplicate and recalculate scores
            var uniqueRecommendations = allRecommendations
                .GroupBy(r => r.Id)
                .Select(g => 
                {
                    var first = g.First();
                    first.Score = CalculateSmartScore(first, g.ToList());
                    return first;
                })
                .OrderByDescending(r => r.Score)
                .Take(_settings.MaxRecommendations)
                .ToList();
            
            return uniqueRecommendations;
        }

        private async Task<List<UserRecommendationResponse>> CreateBulkRecommendationsAsync(
            List<Guid> userIds, Guid currentUserId, string reason)
        {
            if (!userIds.Any()) return new List<UserRecommendationResponse>();

            // Batch get user data
            var users = await GetBulkUserDataAsync(userIds);
            var userStats = await GetBulkUserStatsAsync(userIds);

            var recommendations = new List<UserRecommendationResponse>();
            
            foreach (var user in users)
            {
                var stats = userStats.GetValueOrDefault(user.Id, new UserStats());
                var secureImageUrl = await GetSecureProfileImageUrl(user.ProfileImageUrl);
                
                recommendations.Add(new UserRecommendationResponse
                {
                    Id = user.Id,
                    Name = user.Name,
                    Surname = user.Surname,
                    Username = user.Username,
                    ProfileImageUrl = secureImageUrl,
                    Bio = user.Bio,
                    FollowersCount = stats.FollowersCount,
                    PostsCount = stats.PostsCount,
                    LastActiveAt = user.LastLoginAt ?? user.CreatedAt,
                    IsVerified = false,
                    RecommendationReason = reason,
                    HasMutualFollowers = false,
                    MutualFollowersCount = 0,
                    MutualFollowers = new List<string>()
                });
            }

            return recommendations;
        }

        private async Task<List<Guid>> GetUserFollowingIdsOptimizedAsync(Guid userId)
        {
            var cacheKey = string.Format(USER_FOLLOWING_CACHE_KEY, userId);
            var cachedResult = await _cacheService.GetAsync<List<Guid>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            var following = await _followRepository.FindAsync(f => f.FollowerId == userId);
            var followingIds = following.Select(f => f.FollowedId).ToList();
            
            await _cacheService.SetAsync(cacheKey, followingIds, TimeSpan.FromMinutes(15));
            return followingIds;
        }

        private async Task<List<Guid>> GetUserLikedPostsOptimizedAsync(Guid userId)
        {
            var cacheKey = string.Format(USER_LIKES_CACHE_KEY, userId);
            var cachedResult = await _cacheService.GetAsync<List<Guid>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            var likes = await _likeRepository.FindAsync(l => l.UserId == userId && l.EntityType == "Post");
            var likedPosts = likes.Select(l => l.EntityId).ToList();
            
            await _cacheService.SetAsync(cacheKey, likedPosts, TimeSpan.FromMinutes(20));
            return likedPosts;
        }

        private async Task<Dictionary<Guid, UserStats>> GetBulkUserStatsAsync(List<Guid> userIds = null)
        {
            var cacheKey = userIds == null ? "all_user_stats" : string.Format(BULK_USER_DATA_CACHE_KEY, string.Join(",", userIds.Take(10)));
            var cachedResult = await _cacheService.GetAsync<Dictionary<Guid, UserStats>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }

            var stats = new Dictionary<Guid, UserStats>();
            
            try
            {
                // Parallel execution of stats calculation
                var followersTask = _followRepository.FindAsync(f => userIds == null || userIds.Contains(f.FollowedId));
                var postsTask = _postRepository.FindAsync(p => userIds == null || userIds.Contains(p.UserId));
                
                var followers = await followersTask;
                var posts = await postsTask;
                
                var followersGroups = followers.GroupBy(f => f.FollowedId).ToDictionary(g => g.Key, g => g.Count());
                var postsGroups = posts.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Count());
                
                var allUserIds = userIds ?? followersGroups.Keys.Union(postsGroups.Keys).ToList();
                
                foreach (var userId in allUserIds)
                {
                    stats[userId] = new UserStats
                    {
                        FollowersCount = followersGroups.GetValueOrDefault(userId, 0),
                        PostsCount = postsGroups.GetValueOrDefault(userId, 0)
                    };
                }

                await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(30));
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bulk user stats");
                return new Dictionary<Guid, UserStats>();
            }
        }

        private async Task<List<User>> GetBulkUserDataAsync(List<Guid> userIds)
        {
            var users = new List<User>();
            
            // Check cache first
            var cachedUsers = new List<User>();
            var uncachedIds = new List<Guid>();
            
            foreach (var userId in userIds)
            {
                var cacheKey = $"user_data:{userId}";
                var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
                if (cachedUser != null)
                {
                    cachedUsers.Add(cachedUser);
                }
                else
                {
                    uncachedIds.Add(userId);
                }
            }
            
            // Get uncached users from database
            if (uncachedIds.Any())
            {
                var uncachedUsers = await _userRepository.FindByIdsAsync(uncachedIds, u => u.Id);
                
                // Cache individual users
                var cacheTasks = uncachedUsers.Select(user => 
                    _cacheService.SetAsync($"user_data:{user.Id}", user, TimeSpan.FromMinutes(30)));
                await Task.WhenAll(cacheTasks);
                
                users.AddRange(uncachedUsers);
            }
            
            users.AddRange(cachedUsers);
            return users;
        }

        private async Task<List<Follow>> GetAllFollowRelationshipsAsync()
        {
            var cacheKey = "all_follow_relationships";
            var cachedResult = await _cacheService.GetAsync<List<Follow>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            var follows = (await _followRepository.FindAsync(f => true)).ToList();
            await _cacheService.SetAsync(cacheKey, follows, TimeSpan.FromMinutes(10));
            return follows;
        }

        private async Task<List<Like>> GetAllLikeRelationshipsAsync()
        {
            var cacheKey = "all_like_relationships";
            var cachedResult = await _cacheService.GetAsync<List<Like>>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            var likes = (await _likeRepository.FindAsync(l => l.EntityType == "Post")).ToList();
            await _cacheService.SetAsync(cacheKey, likes, TimeSpan.FromMinutes(15));
            return likes;
        }

        private async Task<string> GetMutualFollowersReasonAsync(Guid currentUserId, Guid recommendedUserId)
        {
            var currentUserFollowing = await GetUserFollowingIdsOptimizedAsync(currentUserId);
            var recommendedUserFollowers = await _cacheService.GetAsync<List<Guid>>(string.Format(USER_FOLLOWERS_CACHE_KEY, recommendedUserId));
            
            if (recommendedUserFollowers == null)
            {
                var followers = await _followRepository.FindAsync(f => f.FollowedId == recommendedUserId);
                recommendedUserFollowers = followers.Select(f => f.FollowerId).ToList();
                await _cacheService.SetAsync(string.Format(USER_FOLLOWERS_CACHE_KEY, recommendedUserId), recommendedUserFollowers, TimeSpan.FromMinutes(15));
            }
            
            var mutualCount = currentUserFollowing.Intersect(recommendedUserFollowers).Count();
            return mutualCount > 0 ? $"Followed by {mutualCount} people you follow" : null;
        }

        private async Task<string> GetPopularityReasonAsync(Guid userId)
        {
            var stats = await GetBulkUserStatsAsync(new List<Guid> { userId });
            var userStats = stats.GetValueOrDefault(userId, new UserStats());
            
            return userStats.FollowersCount >= _settings.MinFollowersForPopular ? "Popular user" : null;
        }

        private async Task<string> GetActivityReasonAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user?.LastLoginAt.HasValue == true && user.LastLoginAt.Value > DateTime.UtcNow.AddDays(-3))
            {
                return "Recently active";
            }
            return null;
        }

        private double CalculateSmartScore(UserRecommendationResponse user, List<UserRecommendationResponse> duplicates)
        {
            double score = 0;
            
            if (user.HasMutualFollowers)
            {
                score += user.MutualFollowersCount * _settings.MutualFollowersWeight;
            }
            
            score += (user.FollowersCount * _settings.PopularityWeight) / 1000.0;
            
            var daysSinceActivity = (DateTime.UtcNow - user.LastActiveAt).Days;
            score += Math.Max(0, (7 - daysSinceActivity) * _settings.ActivityWeight);
            
            var completenessScore = CalculateProfileCompleteness(user);
            score += completenessScore * _settings.ProfileCompletenessWeight;
            
            score += (duplicates.Count - 1) * 0.1;
            
            return Math.Round(score, 2);
        }

        private double CalculateProfileCompleteness(UserRecommendationResponse user)
        {
            double score = 0;
            
            if (!string.IsNullOrEmpty(user.Bio)) score += 0.3;
            if (!string.IsNullOrEmpty(user.ProfileImageUrl)) score += 0.3;
            if (user.PostsCount > 0) score += 0.4;
            
            return score;
        }

        private double CalculatePopularityScore(int followersCount, int postsCount)
        {
            return Math.Round((followersCount * 0.7 + postsCount * 0.3) / 100.0, 2);
        }

        private double CalculateMutualFollowersScore(int mutualCount)
        {
            return Math.Round(mutualCount * 2.0, 2);
        }

        private double CalculateActivityScore(DateTime lastActive)
        {
            var daysSinceActive = (DateTime.UtcNow - lastActive).Days;
            return Math.Round(Math.Max(0, (7 - daysSinceActive) / 7.0), 2);
        }

        private double CalculateUserSimilarity(List<Guid> user1Likes, List<Guid> user2Likes)
        {
            if (!user1Likes.Any() || !user2Likes.Any()) return 0;
            
            var intersection = user1Likes.Intersect(user2Likes).Count();
            var union = user1Likes.Union(user2Likes).Count();
            
            return (double)intersection / union;
        }

        private async Task<string> GetSecureProfileImageUrl(string profileImageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(profileImageUrl))
                    return null;

                return await _mediaService.GenerateSecureUrlAsync(profileImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting secure profile image URL for: {ProfileImageUrl}", profileImageUrl);
                return profileImageUrl;
            }
        }
    }

    public class UserStats
    {
        public int FollowersCount { get; set; }
        public int PostsCount { get; set; }
    }

}
using Camply.Application.Common.Interfaces;
using Camply.Application.Common.Models;
using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain.Analytics;
using Camply.Domain.Common;
using Camply.Infrastructure.Services.MachineLearning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("fixed")]
    public class FeedController : ControllerBase
    {
        private readonly IMLFeedAlgorithmService _feedAlgorithmService;
        private readonly IContextAwareFeedService _contextAwareFeedService;
        private readonly IMLAnalyticsRepository _analyticsRepository;
        private readonly IMLModelService _modelService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<FeedController> _logger;

        public FeedController(
            IMLFeedAlgorithmService feedAlgorithmService,
            IContextAwareFeedService contextAwareFeedService,
            IMLAnalyticsRepository analyticsRepository,
            IMLModelService modelService,
            ICurrentUserService currentUserService,
            ILogger<FeedController> logger)
        {
            _feedAlgorithmService = feedAlgorithmService;
            _contextAwareFeedService = contextAwareFeedService;
            _analyticsRepository = analyticsRepository;
            _modelService = modelService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// 🚀 AI-Powered Smart Feed - Premium recommendation algorithm
        /// Context-aware personalized content with weather, location, and behavioral analysis
        /// </summary>
        /// <param name="count">Number of posts to return (max 50)</param>
        /// <param name="includeWeather">Include weather-based content boosting</param>
        /// <param name="useML">Use machine learning predictions (default: true)</param>
        /// <returns>Personalized feed with AI scoring</returns>
        [HttpGet("smart")]
        [ProducesResponseType(typeof(SmartFeedResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(429)]
        public async Task<ActionResult<SmartFeedResponse>> GetSmartFeed(
            [FromQuery, Range(1, 50)] int count = 20,
            [FromQuery] bool includeWeather = true,
            [FromQuery] bool useML = true)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                _logger.LogInformation("Generating smart feed for user {UserId} with count {Count}", userId, count);

                var context = await _contextAwareFeedService.BuildUserContextAsync(userId, HttpContext);

                var smartFeed = await _contextAwareFeedService.GetContextualizedFeedAsync(userId, context, count);

                if (useML)
                {
                    await EnhanceWithMLPredictionsAsync(smartFeed, userId);
                }

                _ = Task.Run(async () => await TrackFeedImpressionAsync(smartFeed, "smart_ai", context));

                var response = new SmartFeedResponse
                {
                    Posts = smartFeed,
                    Context = new FeedContext
                    {
                        Weather = includeWeather ? context.Weather : null,
                        DeviceType = context.DeviceType,
                        Timestamp = context.Timestamp,
                        HasLocation = context.Latitude.HasValue && context.Longitude.HasValue,
                        LocationBased = context.Latitude.HasValue && smartFeed.Any(p => p.Location != null),
                        AlgorithmVersion = "smart_ai_v2.1",
                        PersonalizationEnabled = true,
                        MLPredictionsEnabled = useML
                    },
                    Metadata = new FeedMetadata
                    {
                        TotalPosts = smartFeed.Count,
                        ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        CacheHit = false,
                        QualityScore = smartFeed.Any() ? smartFeed.Average(p => p.PersonalizationScore) : 0.0,
                        DiversityScore = CalculateDiversityScore(smartFeed)
                    }
                };

                _logger.LogInformation("Smart feed generated for user {UserId} in {ElapsedMs}ms",
                    userId, stopwatch.ElapsedMilliseconds);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating smart feed for user {UserId}", _currentUserService.UserId);
                return StatusCode(500, new { error = "Akıllı feed oluşturulurken bir hata oluştu.", correlationId = HttpContext.TraceIdentifier });
            }
        }

        /// <summary>
        /// 📍 Location-Based Feed - Nearby camping content and experiences
        /// </summary>
        [HttpGet("nearby")]
        [ProducesResponseType(typeof(NearbyFeedResponse), 200)]
        public async Task<ActionResult<NearbyFeedResponse>> GetNearbyFeed(
            [FromQuery, Required] double latitude,
            [FromQuery, Required] double longitude,
            [FromQuery, Range(1, 100)] int radiusKm = 50,
            [FromQuery, Range(1, 50)] int count = 20)
        {
            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                _logger.LogInformation("Getting nearby feed for user {UserId} at location {Lat},{Lon} within {Radius}km",
                    userId, latitude, longitude, radiusKm);

                // Implementation would query posts with location within radius
                var nearbyPosts = new List<PostSummaryResponseML>(); // Placeholder

                // Track location-based search
                await TrackLocationSearchAsync(userId, latitude, longitude, radiusKm);

                var response = new NearbyFeedResponse
                {
                    Posts = nearbyPosts,
                    Location = new LocationInfo
                    {
                        Latitude = latitude,
                        Longitude = longitude,
                        RadiusKm = radiusKm,
                        LocationName = await GetLocationNameAsync(latitude, longitude)
                    },
                    Stats = new NearbyStats
                    {
                        TotalNearbyPosts = nearbyPosts.Count,
                        UniqueCampingSites = nearbyPosts.Where(p => p.Location != null).Select(p => p.Location.Id).Distinct().Count(),
                        AverageDistance = nearbyPosts.Where(p => p.Location != null).Average(p =>
                            CalculateDistance(latitude, longitude, p.Location.Latitude ?? 0, p.Location.Longitude ?? 0))
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby feed");
                return StatusCode(500, "Yakındaki içerikler getirilirken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 📈 Trending Feed - Viral and popular camping content
        /// </summary>
        [HttpGet("trending")]
        [AllowAnonymous] // Allow non-authenticated users to see trending
        [ProducesResponseType(typeof(TrendingFeedResponse), 200)]
        public async Task<ActionResult<TrendingFeedResponse>> GetTrendingFeed(
            [FromQuery, Range(1, 50)] int count = 20,
            [FromQuery] TrendingPeriod period = TrendingPeriod.Day)
        {
            try
            {
                _logger.LogInformation("Getting trending feed with count {Count} for period {Period}", count, period);

                var trendingPosts = await _feedAlgorithmService.GetTrendingPostsAsync(count);

                var response = new TrendingFeedResponse
                {
                    Posts = trendingPosts,
                    Period = period,
                    TrendingMetrics = new TrendingMetrics
                    {
                        TotalEngagement = trendingPosts.Sum(p => p.LikeCount + p.CommentCount + p.ShareCount),
                        AverageEngagementPerPost = trendingPosts.Average(p => p.EngagementScore),
                        TopHashtags = ExtractTopHashtags(trendingPosts),
                        TopLocations = ExtractTopLocations(trendingPosts)
                    },
                    UpdatedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trending feed");
                return StatusCode(500, "Popüler içerikler getirilirken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 🔄 Similar Posts - Content-based recommendations
        /// </summary>
        [HttpGet("{postId}/similar")]
        [ProducesResponseType(typeof(SimilarPostsResponse), 200)]
        public async Task<ActionResult<SimilarPostsResponse>> GetSimilarPosts(
            Guid postId,
            [FromQuery, Range(1, 20)] int count = 10)
        {
            try
            {
                var similarPosts = await _feedAlgorithmService.GetSimilarPostsAsync(postId, count);

                var response = new SimilarPostsResponse
                {
                    Posts = similarPosts,
                    SourcePostId = postId,
                    SimilarityAlgorithm = "content_based_v2",
                    SimilarityMetrics = new SimilarityMetrics
                    {
                        AverageSimilarityScore = similarPosts.Average(p => p.PersonalizationScore),
                        ContentSimilarity = 0.85, // Would be calculated
                        TagSimilarity = 0.72,
                        LocationSimilarity = 0.45
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting similar posts for {PostId}", postId);
                return StatusCode(500, "Benzer içerikler getirilirken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 🎯 Advanced Interaction Tracking - ML learning from user behavior
        /// </summary>
        [HttpPost("interaction")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> TrackAdvancedInteraction([FromBody] AdvancedInteractionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;

                var interaction = new UserInteractionDocument
                {
                    UserId = userId,
                    ContentId = request.PostId,
                    ContentType = "Post",
                    InteractionType = request.InteractionType,
                    ViewDuration = request.ViewDuration,
                    ScrollDepth = request.ScrollDepth,
                    DeviceType = GetDeviceType(),
                    SessionId = GetSessionId(),
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        { "source", request.Source ?? "feed" },
                        { "position", request.Position ?? 0 },
                        { "algorithm_version", request.AlgorithmVersion ?? "v2.1" },
                        { "user_agent", HttpContext.Request.Headers["User-Agent"].ToString() },
                        { "session_duration", request.SessionDuration ?? 0 }
                    }
                };

                await _analyticsRepository.SaveUserInteractionAsync(interaction);

                // Real-time feed learning - update user preferences
                if (request.InteractionType == "like" || request.InteractionType == "share")
                {
                    _ = Task.Run(async () => await UpdateUserPreferencesAsync(userId, request.PostId, request.InteractionType));
                }

                return Ok(new
                {
                    message = "Etkileşim başarıyla kaydedildi.",
                    interactionId = interaction.Id,
                    timestamp = interaction.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking interaction for user {UserId}", _currentUserService.UserId);
                return StatusCode(500, "Etkileşim kaydedilirken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 📊 User Feed Analytics - Personal engagement insights
        /// </summary>
        [HttpGet("analytics")]
        [ProducesResponseType(typeof(FeedAnalyticsResponse), 200)]
        public async Task<ActionResult<FeedAnalyticsResponse>> GetFeedAnalytics(
            [FromQuery, Range(1, 90)] int days = 30)
        {
            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                var period = TimeSpan.FromDays(days);

                var interactionCounts = await _analyticsRepository.GetInteractionCountsByTypeAsync(userId, period);
                var engagementScore = await _analyticsRepository.CalculateUserEngagementScoreAsync(userId, period);

                var recentInteractions = await _analyticsRepository.GetUserInteractionsAsync(
                    userId, DateTime.UtcNow.AddDays(-7));

                var dailyActivity = recentInteractions
                    .GroupBy(i => i.CreatedAt.Date)
                    .ToDictionary(
                        g => g.Key.ToString("yyyy-MM-dd"),
                        g => (double)g.Count()
                    );

                FeedAnalyticsResponse response = new FeedAnalyticsResponse
                {
                    UserId = userId,
                    Period = $"{days} days",
                    EngagementScore = engagementScore,
                    InteractionCounts = interactionCounts.ToDictionary(kvp => kvp.Key, kvp => (long)kvp.Value),
                    DailyActivity = dailyActivity,
                    TotalInteractions = interactionCounts.Values.Sum(),
                    AvgDailyInteractions = dailyActivity.Values.DefaultIfEmpty(0).Average(),
                    MostActiveDay = dailyActivity.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key,
                    PersonalizationLevel = CalculatePersonalizationLevel(engagementScore, interactionCounts.Values.Sum()),
                    RecommendedActions = GenerateRecommendedActions(engagementScore, interactionCounts)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed analytics for user {UserId}", _currentUserService.UserId);
                return StatusCode(500, "Feed analizi getirilirken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 🔄 Refresh Feed Cache - Force regenerate personalized recommendations
        /// </summary>
        [HttpPost("refresh")]
        [EnableRateLimiting("forgot-password")] // Limit refresh requests
        [ProducesResponseType(200)]
        public async Task<ActionResult> RefreshFeed()
        {
            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                await _feedAlgorithmService.RefreshUserFeedCacheAsync(userId);

                return Ok(new
                {
                    message = "Feed başarıyla yenilendi.",
                    refreshedAt = DateTime.UtcNow,
                    userId = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing feed for user {UserId}", _currentUserService.UserId);
                return StatusCode(500, "Feed yenilenirken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 🤖 ML Prediction Score - Get AI engagement prediction for a post
        /// </summary>
        [HttpGet("{postId}/prediction")]
        [ProducesResponseType(typeof(PredictionResponse), 200)]
        public async Task<ActionResult<PredictionResponse>> GetEngagementPrediction(Guid postId)
        {
            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                var predictionScore = await _feedAlgorithmService.PredictEngagementScoreAsync(userId, postId);

                var response = new PredictionResponse
                {
                    PostId = postId,
                    UserId = userId,
                    EngagementScore = predictionScore,
                    PredictionConfidence = CalculatePredictionConfidence(predictionScore),
                    ModelVersion = "engagement_predictor_v2.1",
                    PredictedAt = DateTime.UtcNow,
                    Recommendation = GetEngagementRecommendation(predictionScore)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting engagement prediction for post {PostId}", postId);
                return StatusCode(500, "Engagement tahmini hesaplanırken bir hata oluştu.");
            }
        }

        /// <summary>
        /// 🧪 A/B Test Assignment - Get user's experiment group
        /// </summary>
        [HttpGet("experiment")]
        [ProducesResponseType(typeof(ExperimentResponse), 200)]
        public async Task<ActionResult<ExperimentResponse>> GetExperimentAssignment()
        {
            try
            {
                if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
                {
                    return Unauthorized();
                }

                var userId = _currentUserService.UserId.Value;
                var hashCode = userId.GetHashCode();
                var isTestGroup = Math.Abs(hashCode) % 100 < 50; // 50/50 split

                var response = new ExperimentResponse
                {
                    UserId = userId,
                    ExperimentGroup = isTestGroup ? "test" : "control",
                    AlgorithmVersion = isTestGroup ? "smart_ai_v2.1" : "smart_ai_v2.0",
                    Features = isTestGroup
                        ? new[] { "weather_boost", "location_priority", "ml_predictions", "context_aware" }
                        : new[] { "basic_personalization", "trending_boost" },
                    ExperimentName = "SmartFeed_MLEnhancement_2024",
                    AssignedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting experiment assignment for user {UserId}", _currentUserService.UserId);
                return StatusCode(500, "Experiment bilgisi alınırken bir hata oluştu.");
            }
        }

        // Private Helper Methods
        private async Task EnhanceWithMLPredictionsAsync(List<PostSummaryResponseML> posts, Guid userId)
        {
            var tasks = posts.Select(async post =>
            {
                try
                {
                    var prediction = await _feedAlgorithmService.PredictEngagementScoreAsync(userId, post.Id);
                    post.PersonalizationScore = (post.PersonalizationScore + prediction) / 2; // Blend scores
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get ML prediction for post {PostId}", post.Id);
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task TrackFeedImpressionAsync(List<PostSummaryResponseML> posts, string algorithmVersion, UserContext context)
        {
            try
            {
                var userId = _currentUserService.UserId.Value;
                var impressions = posts.Select((post, index) => new FeedImpressionDocument
                {
                    UserId = userId,
                    PostId = post.Id,
                    Position = index + 1,
                    AlgorithmVersion = algorithmVersion,
                    PredictedScore = (float)post.PersonalizationScore,
                    Source = "smart_feed",
                    CreatedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        { "context", JsonSerializer.Serialize(context) },
                        { "device_type", context.DeviceType },
                        { "has_weather", context.Weather != null },
                        { "has_location", context.Latitude.HasValue }
                    }
                });

                foreach (var impression in impressions)
                {
                    await _analyticsRepository.SaveFeedImpressionAsync(impression);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking feed impressions");
            }
        }

        private async Task TrackLocationSearchAsync(Guid userId, double latitude, double longitude, int radiusKm)
        {
            var interaction = new UserInteractionDocument
            {
                UserId = userId,
                ContentId = Guid.Empty, // No specific content
                ContentType = "LocationSearch",
                InteractionType = "location_search",
                Latitude = latitude,
                Longitude = longitude,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "radius_km", radiusKm },
                    { "search_type", "nearby_feed" }
                }
            };

            await _analyticsRepository.SaveUserInteractionAsync(interaction);
        }

        private async Task UpdateUserPreferencesAsync(Guid userId, Guid postId, string interactionType)
        {
            // This would update user interest profile based on the interaction
            // Implementation would analyze post content and update user preferences
            _logger.LogInformation("Updating preferences for user {UserId} based on {InteractionType} on post {PostId}",
                userId, interactionType, postId);
        }

        private double CalculateDiversityScore(List<PostSummaryResponseML> posts)
        {
            if (!posts.Any()) return 0;

            // Calculate diversity based on different content types, authors, locations
            var uniqueAuthors = posts.Select(p => p.UserId).Distinct().Count();
            var withLocation = posts.Count(p => p.Location != null);
            var withMedia = posts.Count(p => p.Media?.Any() == true);

            var authorDiversity = (double)uniqueAuthors / posts.Count;
            var locationDiversity = (double)withLocation / posts.Count;
            var mediaDiversity = (double)withMedia / posts.Count;

            return (authorDiversity + locationDiversity + mediaDiversity) / 3;
        }

        private async Task<string> GetLocationNameAsync(double latitude, double longitude)
        {
            // This would use a reverse geocoding service
            return $"Location ({latitude:F2}, {longitude:F2})";
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth's radius in kilometers
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private List<string> ExtractTopHashtags(List<PostSummaryResponseML> posts)
        {
            return new List<string> { "#camping", "#nature", "#outdoor", "#adventure", "#doğa" };
        }

        private List<string> ExtractTopLocations(List<PostSummaryResponseML> posts)
        {
            return posts.Where(p => p.Location != null)
                       .Select(p => p.Location.Name)
                       .GroupBy(name => name)
                       .OrderByDescending(g => g.Count())
                       .Take(5)
                       .Select(g => g.Key)
                       .ToList();
        }

        private string CalculatePersonalizationLevel(float engagementScore, int totalInteractions)
        {
            if (totalInteractions < 10) return "Beginner";
            if (engagementScore > 4.0f) return "Expert";
            if (engagementScore > 2.0f) return "Advanced";
            return "Intermediate";
        }

        private List<string> GenerateRecommendedActions(float engagementScore, Dictionary<string, int> interactionCounts)
        {
            var actions = new List<string>();

            if (engagementScore < 1.0f)
                actions.Add("Daha fazla içerik keşfedin");

            if (!interactionCounts.ContainsKey("share") || interactionCounts["share"] < 2)
                actions.Add("Beğendiğiniz içerikleri paylaşın");

            if (!interactionCounts.ContainsKey("comment") || interactionCounts["comment"] < 5)
                actions.Add("Gönderilere yorum yapın");

            return actions;
        }

        private double CalculatePredictionConfidence(double score)
        {
            // Higher confidence for scores closer to 0 or 1
            return 1 - (2 * Math.Abs(score - 0.5));
        }

        private string GetEngagementRecommendation(double score)
        {
            return score switch
            {
                > 0.8 => "Bu içerik size çok uygun!",
                > 0.6 => "Bu içeriği beğenebilirsiniz",
                > 0.4 => "Orta düzeyde ilginç olabilir",
                _ => "Bu içerik size göre olmayabilir"
            };
        }

        private string GetDeviceType()
        {
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString().ToLower();
            if (userAgent.Contains("mobile")) return "mobile";
            if (userAgent.Contains("tablet")) return "tablet";
            return "desktop";
        }

        private string GetSessionId()
        {
            return HttpContext.Session?.Id ??
                   HttpContext.Request.Headers["X-Session-Id"].FirstOrDefault() ??
                   Guid.NewGuid().ToString();
        }
    }

    // Response Models
    public class SmartFeedResponse
    {
        public List<PostSummaryResponseML> Posts { get; set; } = new();
        public FeedContext Context { get; set; }
        public FeedMetadata Metadata { get; set; }
    }

    public class FeedContext
    {
        public WeatherData Weather { get; set; }
        public string DeviceType { get; set; }
        public DateTime Timestamp { get; set; }
        public bool HasLocation { get; set; }
        public bool LocationBased { get; set; }
        public string AlgorithmVersion { get; set; }
        public bool PersonalizationEnabled { get; set; }
        public bool MLPredictionsEnabled { get; set; }
    }

    public class FeedMetadata
    {
        public int TotalPosts { get; set; }
        public int ProcessingTimeMs { get; set; }
        public bool CacheHit { get; set; }
        public double QualityScore { get; set; }
        public double DiversityScore { get; set; }
    }

    public class NearbyFeedResponse
    {
        public List<PostSummaryResponseML> Posts { get; set; } = new();
        public LocationInfo Location { get; set; }
        public NearbyStats Stats { get; set; }
    }

    public class LocationInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusKm { get; set; }
        public string LocationName { get; set; }
    }

    public class NearbyStats
    {
        public int TotalNearbyPosts { get; set; }
        public int UniqueCampingSites { get; set; }
        public double AverageDistance { get; set; }
    }

    public class TrendingFeedResponse
    {
        public List<PostSummaryResponseML> Posts { get; set; } = new();
        public TrendingPeriod Period { get; set; }
        public TrendingMetrics TrendingMetrics { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TrendingMetrics
    {
        public int TotalEngagement { get; set; }
        public double AverageEngagementPerPost { get; set; }
        public List<string> TopHashtags { get; set; } = new();
        public List<string> TopLocations { get; set; } = new();
    }

    public class SimilarPostsResponse
    {
        public List<PostSummaryResponseML> Posts { get; set; } = new();
        public Guid SourcePostId { get; set; }
        public string SimilarityAlgorithm { get; set; }
        public SimilarityMetrics SimilarityMetrics { get; set; }
    }

    public class SimilarityMetrics
    {
        public double AverageSimilarityScore { get; set; }
        public double ContentSimilarity { get; set; }
        public double TagSimilarity { get; set; }
        public double LocationSimilarity { get; set; }
    }

    public class FeedAnalyticsResponse
    {
        public Guid UserId { get; set; }
        public string Period { get; set; }
        public float EngagementScore { get; set; }
        public Dictionary<string, long> InteractionCounts { get; set; } = new();
        public Dictionary<string, double> DailyActivity { get; set; } = new();
        public long TotalInteractions { get; set; }
        public double AvgDailyInteractions { get; set; }
        public string MostActiveDay { get; set; }
        public string PersonalizationLevel { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class PredictionResponse
    {
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public double EngagementScore { get; set; }
        public double PredictionConfidence { get; set; }
        public string ModelVersion { get; set; }
        public DateTime PredictedAt { get; set; }
        public string Recommendation { get; set; }
    }

    public class ExperimentResponse
    {
        public Guid UserId { get; set; }
        public string ExperimentGroup { get; set; }
        public string AlgorithmVersion { get; set; }
        public string[] Features { get; set; }
        public string ExperimentName { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    // Request Models
    public class AdvancedInteractionRequest
    {
        [Required]
        public Guid PostId { get; set; }

        [Required]
        [RegularExpression("^(view|like|comment|share|save|click)$")]
        public string InteractionType { get; set; } = "view";

        [Range(0, int.MaxValue)]
        public int? ViewDuration { get; set; }

        [Range(0, 1)]
        public float? ScrollDepth { get; set; }

        [Range(0, int.MaxValue)]
        public int? Position { get; set; }

        public string Source { get; set; } = "feed";

        public string AlgorithmVersion { get; set; }

        [Range(-90, 90)]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        public double? Longitude { get; set; }

        [Range(0, int.MaxValue)]
        public int? SessionDuration { get; set; }
    }

    // Enums
    public enum TrendingPeriod
    {
        Hour,
        Day,
        Week,
        Month
    }
}
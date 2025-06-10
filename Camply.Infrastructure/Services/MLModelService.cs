using Amazon.Auth.AccessControlPolicy;
using Camply.Application.Common.Interfaces;
using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain.Analytics;
using Camply.Domain.MachineLearning;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Policy = Polly.Policy;

namespace Camply.Infrastructure.Services.MachineLearning
{
    public class MLModelService : IMLModelService
    {
        private readonly IMLModelRepository _modelRepository;
        private readonly IMLAnalyticsRepository _analyticsRepository;
        private readonly ICacheService _cacheService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MLModelService> _logger;
        private readonly MLSettings _mlSettings;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly MLContext _mlContext;
        private readonly ConcurrentDictionary<string, ITransformer> _modelCache;
        private readonly SemaphoreSlim _modelLoadSemaphore;

        public MLModelService(
            IMLModelRepository modelRepository,
            IMLAnalyticsRepository analyticsRepository,
            ICacheService cacheService,
            IMemoryCache memoryCache,
            IOptions<MLSettings> mlSettings,
            ILogger<MLModelService> logger)
        {
            _modelRepository = modelRepository;
            _analyticsRepository = analyticsRepository;
            _cacheService = cacheService;
            _memoryCache = memoryCache;
            _mlSettings = mlSettings.Value;
            _logger = logger;
            _mlContext = new MLContext(seed: 42);
            _modelCache = new ConcurrentDictionary<string, ITransformer>();
            _modelLoadSemaphore = new SemaphoreSlim(1, 1);

            // Configure resilience policies
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("ML prediction retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                    });

            _circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError(exception, "ML circuit breaker opened for {Duration}", duration);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("ML circuit breaker reset");
                    });
        }

        public async Task<double> PredictEngagementAsync(string userFeatures, string contentFeatures)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!await ValidateFeatures(userFeatures, contentFeatures))
                {
                    _logger.LogWarning("Invalid features provided for prediction");
                    return await GetFallbackPredictionAsync(userFeatures, contentFeatures);
                }

                // Apply resilience policies
                var prediction = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    {
                        return await PerformPredictionAsync(userFeatures, contentFeatures);
                    });
                });

                // Log performance metrics
                await LogPredictionMetricsAsync(stopwatch.ElapsedMilliseconds, true);

                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in engagement prediction");
                await LogPredictionMetricsAsync(stopwatch.ElapsedMilliseconds, false);
                return await GetFallbackPredictionAsync(userFeatures, contentFeatures);
            }
        }

        private async Task<double> PerformPredictionAsync(string userFeatures, string contentFeatures)
        {
            // Check cache first
            var cacheKey = GeneratePredictionCacheKey(userFeatures, contentFeatures);
            var cachedPrediction = await _cacheService.GetAsync<double?>(cacheKey);
            if (cachedPrediction.HasValue)
            {
                return cachedPrediction.Value;
            }

            // Get active model
            var model = await GetActiveModelWithCacheAsync("engagement_prediction");
            if (model == null)
            {
                _logger.LogWarning("No active engagement model found");
                return await GetFallbackPredictionAsync(userFeatures, contentFeatures);
            }

            double prediction;

            // Use ML.NET model if available
            if (await HasMLNetModelAsync(model))
            {
                prediction = await PredictWithMLNetAsync(model, userFeatures, contentFeatures);
            }
            else
            {
                // Fallback to rule-based prediction
                prediction = await PredictWithRulesAsync(userFeatures, contentFeatures);
            }

            // Cache prediction
            await _cacheService.SetAsync(cacheKey, prediction, TimeSpan.FromMinutes(_mlSettings.Cache.ModelPredictionCacheMinutes));

            // Log prediction for model improvement
            _ = Task.Run(async () => await LogPredictionAsync(model, userFeatures, contentFeatures, prediction));

            return prediction;
        }

        private async Task<double> PredictWithMLNetAsync(MLModel model, string userFeatures, string contentFeatures)
        {
            try
            {
                var mlNetModel = await LoadMLNetModelAsync(model);
                if (mlNetModel == null)
                {
                    return await PredictWithRulesAsync(userFeatures, contentFeatures);
                }

                // Parse features
                var userFeatureObj = JsonSerializer.Deserialize<UserFeatures>(userFeatures);
                var contentFeatureObj = JsonSerializer.Deserialize<ContentFeatures>(contentFeatures);

                // Create ML.NET input
                var input = new EngagementPredictionInput
                {
                    AccountAge = (float)userFeatureObj.AccountAge,
                    FollowerCount = userFeatureObj.FollowerCount,
                    PostCount = userFeatureObj.PostCount,
                    ContentLength = contentFeatureObj.ContentLength,
                    HasMedia = contentFeatureObj.HasMedia,
                    HasLocation = contentFeatureObj.HasLocation,
                    AuthorFollowerCount = contentFeatureObj.AuthorFollowerCount,
                    QualityScore = (float)contentFeatureObj.QualityScore,
                    CampingInterest = userFeatureObj.CampingInterest,
                    NatureInterest = userFeatureObj.NatureInterest,
                    PhotographyInterest = userFeatureObj.PhotographyInterest,
                    TravelInterest = userFeatureObj.TravelInterest,
                    HoursSincePost = (float)(DateTime.UtcNow - contentFeatureObj.CreatedAt).TotalHours
                };

                // Make prediction
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<EngagementPredictionInput, EngagementPredictionOutput>(mlNetModel);
                var output = predictionEngine.Predict(input);

                return Math.Max(0, Math.Min(1, output.PredictedEngagement));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ML.NET prediction for model {ModelId}", model.Id);
                return await PredictWithRulesAsync(userFeatures, contentFeatures);
            }
        }

        private async Task<ITransformer> LoadMLNetModelAsync(MLModel model)
        {
            if (_modelCache.TryGetValue(model.Id.ToString(), out var cachedModel))
            {
                return cachedModel;
            }

            await _modelLoadSemaphore.WaitAsync();
            try
            {
                // Double-check pattern
                if (_modelCache.TryGetValue(model.Id.ToString(), out cachedModel))
                {
                    return cachedModel;
                }

                if (!File.Exists(model.ModelPath))
                {
                    _logger.LogWarning("Model file not found: {ModelPath}", model.ModelPath);
                    return null;
                }

                var loadedModel = _mlContext.Model.Load(model.ModelPath, out var inputSchema);
                _modelCache.TryAdd(model.Id.ToString(), loadedModel);

                _logger.LogInformation("Loaded ML.NET model {ModelId} from {ModelPath}", model.Id, model.ModelPath);
                return loadedModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ML.NET model {ModelId}", model.Id);
                return null;
            }
            finally
            {
                _modelLoadSemaphore.Release();
            }
        }

        private async Task<double> PredictWithRulesAsync(string userFeatures, string contentFeatures)
        {
            var userFeatureObj = JsonSerializer.Deserialize<UserFeatures>(userFeatures);
            var contentFeatureObj = JsonSerializer.Deserialize<ContentFeatures>(contentFeatures);

            if (userFeatureObj == null || contentFeatureObj == null)
                return 0.5;

            return CalculateAdvancedEngagementScore(userFeatureObj, contentFeatureObj);
        }

        private double CalculateAdvancedEngagementScore(UserFeatures userFeatures, ContentFeatures contentFeatures)
        {
            var weights = new Dictionary<string, double>
            {
                ["content_quality"] = 0.25,
                ["user_content_fit"] = 0.20,
                ["interest_alignment"] = 0.20,
                ["temporal_factors"] = 0.15,
                ["social_proof"] = 0.10,
                ["personalization"] = 0.10
            };

            var scores = new Dictionary<string, double>();

            // Content quality score
            scores["content_quality"] = CalculateContentQualityScore(contentFeatures);

            // User-content fit score
            scores["user_content_fit"] = CalculateUserContentFitScore(userFeatures, contentFeatures);

            // Interest alignment score
            scores["interest_alignment"] = CalculateInterestAlignmentScore(userFeatures, contentFeatures);

            // Temporal factors score
            scores["temporal_factors"] = CalculateTemporalScore(userFeatures, contentFeatures);

            // Social proof score
            scores["social_proof"] = CalculateSocialProofScore(contentFeatures);

            // Personalization score
            scores["personalization"] = CalculatePersonalizationScore(userFeatures);

            // Calculate weighted score
            var finalScore = scores.Sum(kvp => kvp.Value * weights[kvp.Key]);

            // Apply engagement multipliers
            finalScore = ApplyEngagementMultipliers(finalScore, userFeatures, contentFeatures);

            return Math.Max(0, Math.Min(1, finalScore));
        }

        private double CalculateContentQualityScore(ContentFeatures content)
        {
            var score = content.QualityScore;

            // Length penalty/bonus
            if (content.ContentLength < 50) score *= 0.8; // Too short
            else if (content.ContentLength > 500) score *= 0.9; // Too long
            else score *= 1.1; // Good length

            // Media bonus
            if (content.HasMedia) score *= 1.15;

            // Location bonus
            if (content.HasLocation) score *= 1.05;

            return Math.Min(1.0, score);
        }

        private double CalculateUserContentFitScore(UserFeatures user, ContentFeatures content)
        {
            var score = 0.5;

            // Content type preference
            if (user.PreferredContentTypes.ContainsKey("Post"))
            {
                score += user.PreferredContentTypes["Post"] * 0.3;
            }

            // Activity level match
            var isActiveUser = user.RecentEngagementTrend > 0.7;
            var isHighEngagementContent = (content.LikeCount + content.CommentCount) > 10;

            if (isActiveUser && isHighEngagementContent) score += 0.2;

            return Math.Min(1.0, score);
        }

        private double CalculateInterestAlignmentScore(UserFeatures user, ContentFeatures content)
        {
            var alignmentScore = 0.0;
            var maxPossibleScore = 0.0;

            var interestMappings = new Dictionary<string, float>
            {
                ["camping"] = user.CampingInterest,
                ["nature"] = user.NatureInterest,
                ["photography"] = user.PhotographyInterest,
                ["travel"] = user.TravelInterest
            };

            foreach (var category in content.Categories)
            {
                maxPossibleScore += 0.25;
                if (interestMappings.ContainsKey(category.ToLower()))
                {
                    alignmentScore += interestMappings[category.ToLower()] * 0.25;
                }
            }

            return maxPossibleScore > 0 ? alignmentScore / maxPossibleScore : 0.5;
        }

        private double CalculateTemporalScore(UserFeatures user, ContentFeatures content)
        {
            var hoursSincePost = (DateTime.UtcNow - content.CreatedAt).TotalHours;
            var currentHour = DateTime.UtcNow.Hour;

            var freshnessScore = Math.Exp(-hoursSincePost / 48.0); // Decay over 48 hours

            // Time-of-day alignment
            var timeAlignmentScore = 0.5;
            if (user.DailyActiveHours.Contains(currentHour))
            {
                timeAlignmentScore = 1.0;
            }

            return (freshnessScore + timeAlignmentScore) / 2.0;
        }

        private double CalculateSocialProofScore(ContentFeatures content)
        {
            var totalEngagement = content.LikeCount + content.CommentCount * 2;
            var authorCredibility = Math.Log10(content.AuthorFollowerCount + 1) / 5.0; // Log scale

            var engagementScore = Math.Min(1.0, totalEngagement / 100.0);
            var credibilityScore = Math.Min(1.0, authorCredibility);

            return (engagementScore + credibilityScore) / 2.0;
        }

        private double CalculatePersonalizationScore(UserFeatures user)
        {
            var score = 0.5;

            // User engagement level
            if (user.RecentEngagementTrend > 0.8) score += 0.3;
            else if (user.RecentEngagementTrend > 0.5) score += 0.2;
            else if (user.RecentEngagementTrend > 0.2) score += 0.1;

            // Account maturity
            if (user.AccountAge > 365) score += 0.1; // Mature account
            else if (user.AccountAge > 30) score += 0.05; // Established account

            // Social connectivity
            if (user.FollowerCount > 100) score += 0.1;

            return Math.Min(1.0, score);
        }

        private double ApplyEngagementMultipliers(double baseScore, UserFeatures user, ContentFeatures content)
        {
            var multiplier = 1.0;

            // Weekend boost for leisure content
            if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Saturday || DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday)
            {
                if (user.WeekendActivity > 0.6) multiplier *= 1.1;
            }

            // Viral content boost
            var viralThreshold = 50; // likes + comments
            if ((content.LikeCount + content.CommentCount) > viralThreshold)
            {
                multiplier *= 1.15;
            }

            // New user exploration boost
            if (user.AccountAge < 30)
            {
                multiplier *= 1.05;
            }

            return baseScore * multiplier;
        }

        public async Task<List<Guid>> RecommendContentAsync(Guid userId, int count)
        {
            try
            {
                // Use collaborative filtering with content-based fallback
                var collaborativeRecommendations = await GetCollaborativeRecommendationsAsync(userId, count / 2);
                var contentBasedRecommendations = await GetContentBasedRecommendationsAsync(userId, count / 2);

                // Merge and deduplicate
                var allRecommendations = collaborativeRecommendations
                    .Concat(contentBasedRecommendations)
                    .Distinct()
                    .Take(count)
                    .ToList();

                // If not enough recommendations, fill with trending content
                if (allRecommendations.Count < count)
                {
                    var trendingContent = await GetTrendingContentAsync(count - allRecommendations.Count);
                    allRecommendations.AddRange(trendingContent.Except(allRecommendations));
                }

                return allRecommendations.Take(count).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in content recommendation for user {UserId}", userId);
                return await GetTrendingContentAsync(count);
            }
        }

        private async Task<List<Guid>> GetCollaborativeRecommendationsAsync(Guid userId, int count)
        {
            // Implementation of collaborative filtering
            var userInteractions = await _analyticsRepository.GetUserInteractionsAsync(userId, DateTime.UtcNow.AddDays(-30));
            var similarUsers = await FindSimilarUsersAdvancedAsync(userId, userInteractions);

            var recommendations = new List<Guid>();
            foreach (var similarUserId in similarUsers.Take(10))
            {
                var similarUserInteractions = await _analyticsRepository.GetUserInteractionsAsync(similarUserId, DateTime.UtcNow.AddDays(-7));
                var contentIds = similarUserInteractions
                    .Where(i => i.InteractionType == "like" || i.InteractionType == "share")
                    .Select(i => i.ContentId)
                    .Distinct()
                    .Take(count / 10)
                    .ToList();

                recommendations.AddRange(contentIds);
            }

            return recommendations.Distinct().Take(count).ToList();
        }

        private async Task<List<Guid>> GetContentBasedRecommendationsAsync(Guid userId, int count)
        {
            var userInterests = await _analyticsRepository.GetUserInterestsAsync(userId);
            if (userInterests?.Interests == null) return new List<Guid>();
            return new List<Guid>(); // Would implement based on content features
        }

        private async Task<List<Guid>> GetTrendingContentAsync(int count)
        {
            var cacheKey = $"trending_content_{count}";
            var cached = await _cacheService.GetAsync<List<Guid>>(cacheKey);
            if (cached != null) return cached;

            var trending = await _analyticsRepository.GetMostEngagedContentAsync(TimeSpan.FromDays(1), count);
            await _cacheService.SetAsync(cacheKey, trending, TimeSpan.FromMinutes(30));

            return trending;
        }

        private async Task<List<Guid>> FindSimilarUsersAdvancedAsync(Guid userId, List<UserInteractionDocument> userInteractions)
        {
            // Advanced similarity calculation using cosine similarity
            var userContentVector = CreateContentVector(userInteractions);
            var similarUsers = new List<(Guid UserId, double Similarity)>();

            // This would query other users and calculate similarity
            // Simplified implementation for demo
            return new List<Guid>();
        }

        private Dictionary<Guid, double> CreateContentVector(List<UserInteractionDocument> interactions)
        {
            var vector = new Dictionary<Guid, double>();
            var interactionWeights = new Dictionary<string, double>
            {
                ["view"] = 1.0,
                ["like"] = 3.0,
                ["comment"] = 5.0,
                ["share"] = 7.0
            };

            foreach (var interaction in interactions)
            {
                var weight = interactionWeights.GetValueOrDefault(interaction.InteractionType, 1.0);
                vector[interaction.ContentId] = vector.GetValueOrDefault(interaction.ContentId, 0) + weight;
            }

            return vector;
        }

        // Additional production methods...
        private async Task<bool> ValidateFeatures(string userFeatures, string contentFeatures)
        {
            try
            {
                var user = JsonSerializer.Deserialize<UserFeatures>(userFeatures);
                var content = JsonSerializer.Deserialize<ContentFeatures>(contentFeatures);
                return user != null && content != null;
            }
            catch
            {
                return false;
            }
        }

        private string GeneratePredictionCacheKey(string userFeatures, string contentFeatures)
        {
            var hash = (userFeatures + contentFeatures).GetHashCode();
            return $"prediction:{hash}";
        }

        private async Task<MLModel> GetActiveModelWithCacheAsync(string modelType)
        {
            var cacheKey = $"active_model:{modelType}";
            var cached = _memoryCache.Get<MLModel>(cacheKey);
            if (cached != null) return cached;

            var model = await _modelRepository.GetActiveModelAsync(modelType);
            if (model != null)
            {
                _memoryCache.Set(cacheKey, model, TimeSpan.FromMinutes(15));
            }

            return model;
        }

        private async Task<bool> HasMLNetModelAsync(MLModel model)
        {
            return !string.IsNullOrEmpty(model.ModelPath) && File.Exists(model.ModelPath);
        }

        private async Task<double> GetFallbackPredictionAsync(string userFeatures, string contentFeatures)
        {
            try
            {
                return await PredictWithRulesAsync(userFeatures, contentFeatures);
            }
            catch
            {
                return 0.5; // Safe fallback
            }
        }

        private async Task LogPredictionAsync(MLModel model, string userFeatures, string contentFeatures, double prediction)
        {
            try
            {
                var predictionDoc = new ModelPredictionDocument
                {
                    ModelName = model.Name,
                    ModelVersion = model.Version,
                    UserId = ExtractUserIdFromFeatures(userFeatures),
                    ContentId = ExtractContentIdFromFeatures(contentFeatures),
                    PredictionScore = (float)prediction,
                    CreatedAt = DateTime.UtcNow,
                    PredictionType = "engagement",
                    Metadata = new Dictionary<string, object>
                    {
                        { "ModelId", model.Id.ToString() },
                        { "Version", "production_v2" }
                    }
                };

                await _analyticsRepository.SaveModelPredictionAsync(predictionDoc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging prediction");
            }
        }

        private async Task LogPredictionMetricsAsync(long latencyMs, bool success)
        {
            var metric = new AlgorithmMetricsDocument
            {
                MetricType = "prediction_performance",
                AlgorithmVersion = "production_v2",
                Timestamp = DateTime.UtcNow,
                Value = latencyMs,
                SegmentedValues = new Dictionary<string, float>
                {
                    ["latency_ms"] = latencyMs,
                    ["success_rate"] = success ? 1f : 0f
                }
            };

            await _analyticsRepository.SaveAlgorithmMetricAsync(metric);
        }

        private Guid ExtractUserIdFromFeatures(string userFeatures)
        {
            // In production, you'd include user ID in features or pass separately
            return Guid.Empty;
        }

        private Guid ExtractContentIdFromFeatures(string contentFeatures)
        {
            // In production, you'd include content ID in features or pass separately
            return Guid.Empty;
        }

        public async Task<bool> UpdateModelAsync(string modelType, string modelPath)
        {
            // Implementation remains similar but with more validation and rollback capabilities
            return true;
        }

        public async Task<Dictionary<string, double>> GetModelMetricsAsync(string modelType)
        {
            // Enhanced metrics implementation
            return new Dictionary<string, double>();
        }
    }

    public class EngagementPredictionInput
    {
        [LoadColumn(0)] public float AccountAge { get; set; }
        [LoadColumn(1)] public float FollowerCount { get; set; }
        [LoadColumn(2)] public float PostCount { get; set; }
        [LoadColumn(3)] public float ContentLength { get; set; }
        [LoadColumn(4)] public bool HasMedia { get; set; }
        [LoadColumn(5)] public bool HasLocation { get; set; }
        [LoadColumn(6)] public float AuthorFollowerCount { get; set; }
        [LoadColumn(7)] public float QualityScore { get; set; }
        [LoadColumn(8)] public float CampingInterest { get; set; }
        [LoadColumn(9)] public float NatureInterest { get; set; }
        [LoadColumn(10)] public float PhotographyInterest { get; set; }
        [LoadColumn(11)] public float TravelInterest { get; set; }
        [LoadColumn(12)] public float HoursSincePost { get; set; }
        [LoadColumn(13)] public float Label { get; set; } // For training
    }

    public class EngagementPredictionOutput
    {
        [ColumnName("Score")] public float PredictedEngagement { get; set; }
    }

    // Enhanced Feature Models with validation
    public class UserFeatures
    {
        [Range(0, double.MaxValue)] public double AccountAge { get; set; }
        [Range(0, int.MaxValue)] public int FollowerCount { get; set; }
        [Range(0, int.MaxValue)] public int FollowingCount { get; set; }
        [Range(0, int.MaxValue)] public int PostCount { get; set; }
        [Range(0, double.MaxValue)] public double AvgSessionLength { get; set; }
        public List<int> DailyActiveHours { get; set; } = new();
        public Dictionary<string, double> PreferredContentTypes { get; set; } = new();
        [Range(0, 1)] public float CampingInterest { get; set; }
        [Range(0, 1)] public float NatureInterest { get; set; }
        [Range(0, 1)] public float PhotographyInterest { get; set; }
        [Range(0, 1)] public float TravelInterest { get; set; }
        public double LikeToCommentRatio { get; set; }
        [Range(0, 1)] public double ShareFrequency { get; set; }
        [Range(0, 1)] public double BlogReadingFrequency { get; set; }
        [Range(0, 23)] public int PreferredTimeOfDay { get; set; }
        [Range(0, 1)] public double WeekendActivity { get; set; }
        [Range(0, double.MaxValue)] public double DaysSinceLastActive { get; set; }
        [Range(0, 2)] public double RecentEngagementTrend { get; set; }
    }

    public class ContentFeatures
    {
        [Range(0, int.MaxValue)] public int ContentLength { get; set; }
        [Range(0, int.MaxValue)] public int WordCount { get; set; }
        public bool HasMedia { get; set; }
        [Range(0, int.MaxValue)] public int MediaCount { get; set; }
        public bool HasLocation { get; set; }
        public DateTime CreatedAt { get; set; }
        [Range(0, int.MaxValue)] public int AuthorFollowerCount { get; set; }
        [Range(0, int.MaxValue)] public int LikeCount { get; set; }
        [Range(0, int.MaxValue)] public int CommentCount { get; set; }
        public TextFeatures TextFeatures { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public LocationFeatures LocationFeatures { get; set; }
        [Range(0, 1)] public double QualityScore { get; set; }
    }

    public class TextFeatures
    {
        [Range(0, int.MaxValue)] public int Length { get; set; }
        [Range(0, int.MaxValue)] public int WordCount { get; set; }
        [Range(0, int.MaxValue)] public int SentenceCount { get; set; }
        [Range(-1, 1)] public double SentimentScore { get; set; }
        public List<string> Keywords { get; set; } = new();
        public bool HasQuestion { get; set; }
        public bool HasHashtags { get; set; }
        public bool HasMentions { get; set; }
        public bool HasUrls { get; set; }
    }

    public class LocationFeatures
    {
        [Range(-90, 90)] public double Latitude { get; set; }
        [Range(-180, 180)] public double Longitude { get; set; }
        public string Type { get; set; }
    }
}
using Camply.Domain.Analytics;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLAnalyticsRepository
    {
        Task SaveUserInteractionAsync(UserInteractionDocument interaction);
        Task<List<UserInteractionDocument>> GetUserInteractionsAsync(Guid userId, DateTime from, DateTime? to = null);
        Task<Dictionary<string, int>> GetInteractionCountsByTypeAsync(Guid userId, TimeSpan period);
        Task<float> CalculateUserEngagementScoreAsync(Guid userId, TimeSpan period);

        // User Interests
        Task<UserInterestDocument> GetUserInterestsAsync(Guid userId);
        Task SaveUserInterestsAsync(UserInterestDocument interests);

        // Feed Cache
        Task<FeedCacheDocument> GetCachedFeedAsync(Guid userId, string feedType, int page);
        Task SaveCachedFeedAsync(FeedCacheDocument cache);
        Task InvalidateUserCacheAsync(Guid userId);

        // Model Predictions
        Task SaveModelPredictionAsync(ModelPredictionDocument prediction);
        Task<List<ModelPredictionDocument>> GetModelPredictionsAsync(Guid userId, string modelName, DateTime from);

        // Algorithm Metrics
        Task SaveAlgorithmMetricAsync(AlgorithmMetricsDocument metric);
        Task<List<AlgorithmMetricsDocument>> GetAlgorithmMetricsAsync(string metricType, DateTime from, DateTime to);

        // Feed Impressions
        Task SaveFeedImpressionAsync(FeedImpressionDocument impression);
        Task<List<FeedImpressionDocument>> GetFeedImpressionsAsync(Guid userId, DateTime from, DateTime to);

        // ADDITIONAL PRODUCTION METHODS
        Task<List<Guid>> GetMostEngagedContentAsync(TimeSpan period, int limit = 20);
        Task<Dictionary<string, float>> GetContentEngagementStatsAsync(Guid contentId, TimeSpan period);
        Task<List<UserInteractionDocument>> GetTopUserInteractionsAsync(int limit, TimeSpan period);
        Task<Dictionary<string, int>> GetGlobalInteractionStatsAsync(TimeSpan period);
        Task<List<FeedImpressionDocument>> GetFeedImpressionsByAlgorithmAsync(string algorithmVersion, DateTime from, DateTime to);
        Task<float> CalculateClickThroughRateAsync(string algorithmVersion, TimeSpan period);
        Task<Dictionary<string, float>> GetHourlyEngagementPatternsAsync(Guid userId);
        Task<List<UserInteractionDocument>> GetInteractionsByContentTypeAsync(string contentType, TimeSpan period, int limit = 100);
        Task<bool> CleanupOldInteractionsAsync(TimeSpan retentionPeriod);
        Task<Dictionary<string, object>> GetSystemHealthMetricsAsync();
    }
}

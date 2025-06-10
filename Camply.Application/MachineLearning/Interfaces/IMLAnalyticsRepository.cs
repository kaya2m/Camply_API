using Camply.Domain.Analytics;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLAnalyticsRepository
    {
        Task SaveUserInteractionAsync(UserInteractionDocument interaction);
        Task<List<UserInteractionDocument>> GetUserInteractionsAsync(Guid userId, DateTime from, DateTime? to = null);
        Task<UserInterestDocument> GetUserInterestsAsync(Guid userId);
        Task SaveUserInterestsAsync(UserInterestDocument interests);
        Task<FeedCacheDocument> GetCachedFeedAsync(Guid userId, string feedType, int page);
        Task SaveCachedFeedAsync(FeedCacheDocument cache);
        Task InvalidateUserCacheAsync(Guid userId);
        Task SaveModelPredictionAsync(ModelPredictionDocument prediction);
        Task<List<ModelPredictionDocument>> GetModelPredictionsAsync(Guid userId, string modelName, DateTime from);
        Task SaveAlgorithmMetricAsync(AlgorithmMetricsDocument metric);
        Task<List<AlgorithmMetricsDocument>> GetAlgorithmMetricsAsync(string metricType, DateTime from, DateTime to);
        Task SaveFeedImpressionAsync(FeedImpressionDocument impression);
        Task<List<FeedImpressionDocument>> GetFeedImpressionsAsync(Guid userId, DateTime from, DateTime to);
    }
}

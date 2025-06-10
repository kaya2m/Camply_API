using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain.Analytics;
using Camply.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Repositories.MachineLearning
{
    public class MLAnalyticsRepository : IMLAnalyticsRepository
    {
        private readonly MongoDbContext _mongoContext;
        private readonly ILogger<MLAnalyticsRepository> _logger;

        public MLAnalyticsRepository(MongoDbContext mongoContext, ILogger<MLAnalyticsRepository> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        public async Task SaveUserInteractionAsync(UserInteractionDocument interaction)
        {
            try
            {
                interaction.CreatedAt = DateTime.UtcNow;
                await _mongoContext.UserInteractions.InsertOneAsync(interaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user interaction for user {UserId}", interaction.UserId);
                throw;
            }
        }

        public async Task<List<UserInteractionDocument>> GetUserInteractionsAsync(Guid userId, DateTime from, DateTime? to = null)
        {
            var toDate = to ?? DateTime.UtcNow;

            return await _mongoContext.UserInteractions
                .Find(ui => ui.UserId == userId && ui.CreatedAt >= from && ui.CreatedAt <= toDate)
                .SortByDescending(ui => ui.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserInterestDocument> GetUserInterestsAsync(Guid userId)
        {
            return await _mongoContext.UserInterests
                .Find(ui => ui.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task SaveUserInterestsAsync(UserInterestDocument interests)
        {
            interests.UpdatedAt = DateTime.UtcNow;

            var filter = Builders<UserInterestDocument>.Filter.Eq(ui => ui.UserId, interests.UserId);
            var options = new ReplaceOptions { IsUpsert = true };

            await _mongoContext.UserInterests.ReplaceOneAsync(filter, interests, options);
        }

        public async Task<FeedCacheDocument> GetCachedFeedAsync(Guid userId, string feedType, int page)
        {
            return await _mongoContext.FeedCache
                .Find(fc => fc.UserId == userId && fc.FeedType == feedType && fc.Page == page && fc.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();
        }

        public async Task SaveCachedFeedAsync(FeedCacheDocument cache)
        {
            cache.CreatedAt = DateTime.UtcNow;
            if (cache.ExpiresAt == default)
                cache.ExpiresAt = DateTime.UtcNow.AddMinutes(15); // Default 15 minutes

            await _mongoContext.FeedCache.InsertOneAsync(cache);
        }

        public async Task InvalidateUserCacheAsync(Guid userId)
        {
            var filter = Builders<FeedCacheDocument>.Filter.Eq(fc => fc.UserId, userId);
            await _mongoContext.FeedCache.DeleteManyAsync(filter);
        }

        public async Task SaveModelPredictionAsync(ModelPredictionDocument prediction)
        {
            prediction.CreatedAt = DateTime.UtcNow;
            await _mongoContext.ModelPredictions.InsertOneAsync(prediction);
        }

        public async Task<List<ModelPredictionDocument>> GetModelPredictionsAsync(Guid userId, string modelName, DateTime from)
        {
            return await _mongoContext.ModelPredictions
                .Find(mp => mp.UserId == userId && mp.ModelName == modelName && mp.CreatedAt >= from)
                .SortByDescending(mp => mp.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveAlgorithmMetricAsync(AlgorithmMetricsDocument metric)
        {
            metric.Timestamp = DateTime.UtcNow;
            await _mongoContext.AlgorithmMetrics.InsertOneAsync(metric);
        }

        public async Task<List<AlgorithmMetricsDocument>> GetAlgorithmMetricsAsync(string metricType, DateTime from, DateTime to)
        {
            return await _mongoContext.AlgorithmMetrics
                .Find(am => am.MetricType == metricType && am.Timestamp >= from && am.Timestamp <= to)
                .SortByDescending(am => am.Timestamp)
                .ToListAsync();
        }

        public async Task SaveFeedImpressionAsync(FeedImpressionDocument impression)
        {
            impression.CreatedAt = DateTime.UtcNow;
            await _mongoContext.FeedImpressions.InsertOneAsync(impression);
        }

        public async Task<List<FeedImpressionDocument>> GetFeedImpressionsAsync(Guid userId, DateTime from, DateTime to)
        {
            return await _mongoContext.FeedImpressions
                .Find(fi => fi.UserId == userId && fi.CreatedAt >= from && fi.CreatedAt <= to)
                .SortByDescending(fi => fi.CreatedAt)
                .ToListAsync();
        }
    }
}

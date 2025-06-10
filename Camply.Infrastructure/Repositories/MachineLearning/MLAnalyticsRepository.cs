using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain.Analytics;
using Camply.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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

                _logger.LogDebug("Saved user interaction for user {UserId}, content {ContentId}, type {InteractionType}",
                    interaction.UserId, interaction.ContentId, interaction.InteractionType);
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

            var filter = Builders<UserInteractionDocument>.Filter.And(
                Builders<UserInteractionDocument>.Filter.Eq(ui => ui.UserId, userId),
                Builders<UserInteractionDocument>.Filter.Gte(ui => ui.CreatedAt, from),
                Builders<UserInteractionDocument>.Filter.Lte(ui => ui.CreatedAt, toDate)
            );

            return await _mongoContext.UserInteractions
                .Find(filter)
                .SortByDescending(ui => ui.CreatedAt)
                .Limit(1000)
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
            var filter = Builders<FeedCacheDocument>.Filter.And(
                Builders<FeedCacheDocument>.Filter.Eq(fc => fc.UserId, userId),
                Builders<FeedCacheDocument>.Filter.Eq(fc => fc.FeedType, feedType),
                Builders<FeedCacheDocument>.Filter.Eq(fc => fc.Page, page),
                Builders<FeedCacheDocument>.Filter.Gt(fc => fc.ExpiresAt, DateTime.UtcNow)
            );

            return await _mongoContext.FeedCache.Find(filter).FirstOrDefaultAsync();
        }

        public async Task SaveCachedFeedAsync(FeedCacheDocument cache)
        {
            cache.CreatedAt = DateTime.UtcNow;
            if (cache.ExpiresAt == default)
                cache.ExpiresAt = DateTime.UtcNow.AddMinutes(15);

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
            var filter = Builders<ModelPredictionDocument>.Filter.And(
                Builders<ModelPredictionDocument>.Filter.Eq(mp => mp.UserId, userId),
                Builders<ModelPredictionDocument>.Filter.Eq(mp => mp.ModelName, modelName),
                Builders<ModelPredictionDocument>.Filter.Gte(mp => mp.CreatedAt, from)
            );

            return await _mongoContext.ModelPredictions
                .Find(filter)
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
            var filter = Builders<AlgorithmMetricsDocument>.Filter.And(
                Builders<AlgorithmMetricsDocument>.Filter.Eq(am => am.MetricType, metricType),
                Builders<AlgorithmMetricsDocument>.Filter.Gte(am => am.Timestamp, from),
                Builders<AlgorithmMetricsDocument>.Filter.Lte(am => am.Timestamp, to)
            );

            return await _mongoContext.AlgorithmMetrics
                .Find(filter)
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
            var filter = Builders<FeedImpressionDocument>.Filter.And(
                Builders<FeedImpressionDocument>.Filter.Eq(fi => fi.UserId, userId),
                Builders<FeedImpressionDocument>.Filter.Gte(fi => fi.CreatedAt, from),
                Builders<FeedImpressionDocument>.Filter.Lte(fi => fi.CreatedAt, to)
            );

            return await _mongoContext.FeedImpressions
                .Find(filter)
                .SortByDescending(fi => fi.CreatedAt)
                .ToListAsync();
        }


        #region Enhanced Analytics Methods

        public async Task<Dictionary<string, int>> GetInteractionCountsByTypeAsync(Guid userId, TimeSpan period)
        {
            var from = DateTime.UtcNow - period;
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    {"UserId", BsonValue.Create(userId)},
                    {"CreatedAt", new BsonDocument("$gte", from)}
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    {"_id", "$InteractionType"},
                    {"count", new BsonDocument("$sum", 1)}
                })
            };

            var results = await _mongoContext.UserInteractions.Aggregate<BsonDocument>(pipeline).ToListAsync();
            return results.ToDictionary(
                doc => doc["_id"].AsString,
                doc => doc["count"].AsInt32
            );
        }

        public async Task<float> CalculateUserEngagementScoreAsync(Guid userId, TimeSpan period)
        {
            var interactions = await GetUserInteractionsAsync(userId, DateTime.UtcNow - period);

            if (!interactions.Any()) return 0f;

            var score = 0f;
            foreach (var interaction in interactions)
            {
                score += interaction.InteractionType switch
                {
                    "view" => 1f,
                    "like" => 3f,
                    "comment" => 5f,
                    "share" => 7f,
                    "save" => 4f,
                    _ => 0f
                };

                // Add time spent bonus
                if (interaction.ViewDuration.HasValue)
                {
                    score += Math.Min(interaction.ViewDuration.Value / 60f, 5f);
                }
            }

            return score / (float)period.TotalDays;
        }

        public async Task<List<Guid>> GetMostEngagedContentAsync(TimeSpan period, int limit = 20)
        {
            var from = DateTime.UtcNow - period;
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    {"CreatedAt", new BsonDocument("$gte", from)},
                    {"InteractionType", new BsonDocument("$in", new BsonArray {"like", "comment", "share"})}
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    {"_id", "$ContentId"},
                    {"engagementScore", new BsonDocument("$sum", new BsonDocument("$switch", new BsonDocument
                    {
                        {"branches", new BsonArray
                        {
                            new BsonDocument {{"case", new BsonDocument("$eq", new BsonArray {"$InteractionType", "view"})}, {"then", 1}},
                            new BsonDocument {{"case", new BsonDocument("$eq", new BsonArray {"$InteractionType", "like"})}, {"then", 3}},
                            new BsonDocument {{"case", new BsonDocument("$eq", new BsonArray {"$InteractionType", "comment"})}, {"then", 5}},
                            new BsonDocument {{"case", new BsonDocument("$eq", new BsonArray {"$InteractionType", "share"})}, {"then", 7}}
                        }},
                        {"default", 0}
                    }))}
                }),
                new BsonDocument("$sort", new BsonDocument("engagementScore", -1)),
                new BsonDocument("$limit", limit)
            };

            var results = await _mongoContext.UserInteractions.Aggregate<BsonDocument>(pipeline).ToListAsync();
            return results.Select(doc => Guid.Parse(doc["_id"].AsString)).ToList();
        }

        public async Task<Dictionary<string, float>> GetContentEngagementStatsAsync(Guid contentId, TimeSpan period)
        {
            var from = DateTime.UtcNow - period;
            var filter = Builders<UserInteractionDocument>.Filter.And(
                Builders<UserInteractionDocument>.Filter.Eq(ui => ui.ContentId, contentId),
                Builders<UserInteractionDocument>.Filter.Gte(ui => ui.CreatedAt, from)
            );

            var interactions = await _mongoContext.UserInteractions.Find(filter).ToListAsync();

            if (!interactions.Any())
            {
                return new Dictionary<string, float>
                {
                    ["total_interactions"] = 0,
                    ["unique_users"] = 0,
                    ["engagement_rate"] = 0,
                    ["avg_view_time"] = 0
                };
            }

            var uniqueUsers = interactions.Select(i => i.UserId).Distinct().Count();
            var totalViews = interactions.Count(i => i.InteractionType == "view");
            var totalEngagements = interactions.Count(i => i.InteractionType != "view");
            var avgViewTime = interactions
                .Where(i => i.ViewDuration.HasValue)
                .Average(i => i.ViewDuration.Value);

            return new Dictionary<string, float>
            {
                ["total_interactions"] = interactions.Count,
                ["unique_users"] = uniqueUsers,
                ["engagement_rate"] = totalViews > 0 ? (float)totalEngagements / totalViews : 0,
                ["avg_view_time"] = (float)avgViewTime
            };
        }

        public async Task<List<UserInteractionDocument>> GetTopUserInteractionsAsync(int limit, TimeSpan period)
        {
            var from = DateTime.UtcNow - period;
            var filter = Builders<UserInteractionDocument>.Filter.Gte(ui => ui.CreatedAt, from);

            return await _mongoContext.UserInteractions
                .Find(filter)
                .SortByDescending(ui => ui.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetGlobalInteractionStatsAsync(TimeSpan period)
        {
            var from = DateTime.UtcNow - period;
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    {"CreatedAt", new BsonDocument("$gte", from)}
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    {"_id", "$InteractionType"},
                    {"count", new BsonDocument("$sum", 1)}
                })
            };

            var results = await _mongoContext.UserInteractions.Aggregate<BsonDocument>(pipeline).ToListAsync();
            return results.ToDictionary(
                doc => doc["_id"].AsString,
                doc => doc["count"].AsInt32
            );
        }

        public async Task<List<FeedImpressionDocument>> GetFeedImpressionsByAlgorithmAsync(string algorithmVersion, DateTime from, DateTime to)
        {
            var filter = Builders<FeedImpressionDocument>.Filter.And(
                Builders<FeedImpressionDocument>.Filter.Eq(fi => fi.AlgorithmVersion, algorithmVersion),
                Builders<FeedImpressionDocument>.Filter.Gte(fi => fi.CreatedAt, from),
                Builders<FeedImpressionDocument>.Filter.Lte(fi => fi.CreatedAt, to)
            );

            return await _mongoContext.FeedImpressions
                .Find(filter)
                .SortByDescending(fi => fi.CreatedAt)
                .ToListAsync();
        }

        public async Task<float> CalculateClickThroughRateAsync(string algorithmVersion, TimeSpan period)
        {
            var from = DateTime.UtcNow - period;
            var impressions = await GetFeedImpressionsByAlgorithmAsync(algorithmVersion, from, DateTime.UtcNow);

            if (!impressions.Any()) return 0f;

            var totalImpressions = impressions.Count;
            var totalClicks = impressions.Count(i => i.WasClicked);

            return (float)totalClicks / totalImpressions * 100;
        }

        public async Task<Dictionary<string, float>> GetHourlyEngagementPatternsAsync(Guid userId)
        {
            var from = DateTime.UtcNow.AddDays(-30); // Last 30 days
            var filter = Builders<UserInteractionDocument>.Filter.And(
                Builders<UserInteractionDocument>.Filter.Eq(ui => ui.UserId, userId),
                Builders<UserInteractionDocument>.Filter.Gte(ui => ui.CreatedAt, from)
            );

            var interactions = await _mongoContext.UserInteractions.Find(filter).ToListAsync();

            var hourlyPatterns = new Dictionary<string, float>();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourlyInteractions = interactions.Count(i => i.CreatedAt.Hour == hour);
                hourlyPatterns[hour.ToString("00")] = hourlyInteractions;
            }

            return hourlyPatterns;
        }

        public async Task<List<UserInteractionDocument>> GetInteractionsByContentTypeAsync(string contentType, TimeSpan period, int limit = 100)
        {
            var from = DateTime.UtcNow - period;
            var filter = Builders<UserInteractionDocument>.Filter.And(
                Builders<UserInteractionDocument>.Filter.Eq(ui => ui.ContentType, contentType),
                Builders<UserInteractionDocument>.Filter.Gte(ui => ui.CreatedAt, from)
            );

            return await _mongoContext.UserInteractions
                .Find(filter)
                .SortByDescending(ui => ui.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> CleanupOldInteractionsAsync(TimeSpan retentionPeriod)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow - retentionPeriod;

                // Delete old interactions
                var interactionFilter = Builders<UserInteractionDocument>.Filter.Lt(ui => ui.CreatedAt, cutoffDate);
                var interactionResult = await _mongoContext.UserInteractions.DeleteManyAsync(interactionFilter);

                // Delete old impressions
                var impressionFilter = Builders<FeedImpressionDocument>.Filter.Lt(fi => fi.CreatedAt, cutoffDate);
                var impressionResult = await _mongoContext.FeedImpressions.DeleteManyAsync(impressionFilter);

                // Delete old model predictions
                var predictionFilter = Builders<ModelPredictionDocument>.Filter.Lt(mp => mp.CreatedAt, cutoffDate);
                var predictionResult = await _mongoContext.ModelPredictions.DeleteManyAsync(predictionFilter);

                _logger.LogInformation("Cleanup completed: {InteractionCount} interactions, {ImpressionCount} impressions, {PredictionCount} predictions deleted",
                    interactionResult.DeletedCount, impressionResult.DeletedCount, predictionResult.DeletedCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup operation");
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetSystemHealthMetricsAsync()
        {
            try
            {
                var last24Hours = DateTime.UtcNow.AddDays(-1);

                // Count documents in each collection
                var interactionCount = await _mongoContext.UserInteractions.CountDocumentsAsync(FilterDefinition<UserInteractionDocument>.Empty);
                var impressionCount = await _mongoContext.FeedImpressions.CountDocumentsAsync(FilterDefinition<FeedImpressionDocument>.Empty);
                var predictionCount = await _mongoContext.ModelPredictions.CountDocumentsAsync(FilterDefinition<ModelPredictionDocument>.Empty);
                var cacheCount = await _mongoContext.FeedCache.CountDocumentsAsync(FilterDefinition<FeedCacheDocument>.Empty);

                // Recent activity
                var recentInteractions = await _mongoContext.UserInteractions
                    .CountDocumentsAsync(ui => ui.CreatedAt >= last24Hours);

                var recentImpressions = await _mongoContext.FeedImpressions
                    .CountDocumentsAsync(fi => fi.CreatedAt >= last24Hours);

                return new Dictionary<string, object>
                {
                    ["total_interactions"] = interactionCount,
                    ["total_impressions"] = impressionCount,
                    ["total_predictions"] = predictionCount,
                    ["total_cache_entries"] = cacheCount,
                    ["interactions_last_24h"] = recentInteractions,
                    ["impressions_last_24h"] = recentImpressions,
                    ["system_status"] = "healthy",
                    ["last_updated"] = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system health metrics");
                return new Dictionary<string, object>
                {
                    ["system_status"] = "error",
                    ["error_message"] = ex.Message,
                    ["last_updated"] = DateTime.UtcNow
                };
            }
        }

        #endregion
    }
}

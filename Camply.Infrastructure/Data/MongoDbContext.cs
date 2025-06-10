using Camply.Domain.Analytics;
using Camply.Domain.Messages;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly MongoClient _client;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            _client = new MongoClient(settings.Value.ConnectionString);
            _database = _client.GetDatabase(settings.Value.DatabaseName);
        }

        // Koleksiyonlar
        public IMongoCollection<Conversation> Conversations =>
            _database.GetCollection<Conversation>("Conversations");

        public IMongoCollection<Message> Messages =>
            _database.GetCollection<Message>("Messages");

        public IMongoCollection<Reaction> Reactions =>
            _database.GetCollection<Reaction>("Reactions");
        public IMongoCollection<UserInteractionDocument> UserInteractions =>
           _database.GetCollection<UserInteractionDocument>("UserInteractions");

        public IMongoCollection<UserInterestDocument> UserInterests =>
            _database.GetCollection<UserInterestDocument>("UserInterests");

        public IMongoCollection<FeedCacheDocument> FeedCache =>
            _database.GetCollection<FeedCacheDocument>("FeedCache");

        public IMongoCollection<ModelPredictionDocument> ModelPredictions =>
            _database.GetCollection<ModelPredictionDocument>("ModelPredictions");

        public IMongoCollection<AlgorithmMetricsDocument> AlgorithmMetrics =>
            _database.GetCollection<AlgorithmMetricsDocument>("AlgorithmMetrics");

        public IMongoCollection<FeedImpressionDocument> FeedImpressions =>
            _database.GetCollection<FeedImpressionDocument>("FeedImpressions");

        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                await _database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task InitializeAsync()
        {
            var collectionNames = await (await _database.ListCollectionNamesAsync()).ToListAsync();

            if (!collectionNames.Contains("Conversations"))
            {
                await _database.CreateCollectionAsync("Conversations");

                await Conversations.Indexes.CreateOneAsync(
                    new CreateIndexModel<Conversation>(
                        Builders<Conversation>.IndexKeys.Ascending(c => c.ParticipantIds)));

                await Conversations.Indexes.CreateOneAsync(
                    new CreateIndexModel<Conversation>(
                        Builders<Conversation>.IndexKeys.Descending(c => c.LastActivityDate)));

                await Conversations.Indexes.CreateOneAsync(
                    new CreateIndexModel<Conversation>(
                        Builders<Conversation>.IndexKeys
                            .Ascending(c => c.ParticipantIds)
                            .Descending(c => c.LastActivityDate),
                        new CreateIndexOptions { Background = true, Name = "ix_participants_lastactivity" }
                    )
                );

                await Conversations.Indexes.CreateOneAsync(
                    new CreateIndexModel<Conversation>(
                        Builders<Conversation>.IndexKeys.Ascending(c => c.Status),
                        new CreateIndexOptions { Background = true, Name = "ix_status" }
                    )
                );
            }
            else
            {
                var conversationIndexes = await (await Conversations.Indexes.ListAsync()).ToListAsync();
                var indexNames = conversationIndexes.Select(idx => idx["name"].AsString).ToList();

                if (!indexNames.Contains("ix_participants_lastactivity"))
                {
                    await Conversations.Indexes.CreateOneAsync(
                        new CreateIndexModel<Conversation>(
                            Builders<Conversation>.IndexKeys
                                .Ascending(c => c.ParticipantIds)
                                .Descending(c => c.LastActivityDate),
                            new CreateIndexOptions { Background = true, Name = "ix_participants_lastactivity" }
                        )
                    );
                }

                if (!indexNames.Contains("ix_status"))
                {
                    await Conversations.Indexes.CreateOneAsync(
                        new CreateIndexModel<Conversation>(
                            Builders<Conversation>.IndexKeys.Ascending(c => c.Status),
                            new CreateIndexOptions { Background = true, Name = "ix_status" }
                        )
                    );
                }
            }

            if (!collectionNames.Contains("Messages"))
            {
                await _database.CreateCollectionAsync("Messages");

                await Messages.Indexes.CreateOneAsync(
                    new CreateIndexModel<Message>(
                        Builders<Message>.IndexKeys
                            .Ascending(m => m.ConversationId)
                            .Descending(m => m.CreatedAt)));

                await Messages.Indexes.CreateOneAsync(
                    new CreateIndexModel<Message>(
                        Builders<Message>.IndexKeys.Ascending(m => m.SenderId)));
            }

            if (!collectionNames.Contains("Reactions"))
            {
                await _database.CreateCollectionAsync("Reactions");

                await Reactions.Indexes.CreateOneAsync(
                    new CreateIndexModel<Reaction>(
                        Builders<Reaction>.IndexKeys.Ascending(r => r.MessageId)));

                await Reactions.Indexes.CreateOneAsync(
                    new CreateIndexModel<Reaction>(
                        Builders<Reaction>.IndexKeys.Ascending(r => r.UserId)));
            }

            if (_database.GetCollection<BsonDocument>("Users") != null)
            {
                var users = _database.GetCollection<BsonDocument>("Users");
                var userIndexes = await (await users.Indexes.ListAsync()).ToListAsync();
                var userIndexNames = userIndexes.Select(idx => idx["name"].AsString).ToList();

                if (!userIndexNames.Contains("ix_id"))
                {
                    await users.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(
                            Builders<BsonDocument>.IndexKeys.Ascending("Id"),
                            new CreateIndexOptions { Background = true, Name = "ix_id" }
                        )
                    );
                }

                if (!userIndexNames.Contains("ix_username"))
                {
                    await users.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(
                            Builders<BsonDocument>.IndexKeys.Ascending("Username"),
                            new CreateIndexOptions { Background = true, Name = "ix_username" }
                        )
                    );
                }
            }

            await UserInteractions.Indexes.CreateManyAsync(new[]
          {
                new CreateIndexModel<UserInteractionDocument>(
                    Builders<UserInteractionDocument>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_user_interactions_user_time", Background = true }),

                new CreateIndexModel<UserInteractionDocument>(
                    Builders<UserInteractionDocument>.IndexKeys
                        .Ascending(x => x.ContentId)
                        .Ascending(x => x.ContentType),
                    new CreateIndexOptions { Name = "ix_user_interactions_content", Background = true }),

                new CreateIndexModel<UserInteractionDocument>(
                    Builders<UserInteractionDocument>.IndexKeys.Ascending(x => x.InteractionType),
                    new CreateIndexOptions { Name = "ix_user_interactions_type", Background = true }),

                new CreateIndexModel<UserInteractionDocument>(
                    Builders<UserInteractionDocument>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_user_interactions_time", Background = true })
            });

            // UserInterests indexes
            await UserInterests.Indexes.CreateOneAsync(
                new CreateIndexModel<UserInterestDocument>(
                    Builders<UserInterestDocument>.IndexKeys.Ascending(x => x.UserId),
                    new CreateIndexOptions { Name = "ix_user_interests_user", Background = true, Unique = true }));

            // FeedCache indexes
            await FeedCache.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<FeedCacheDocument>(
                    Builders<FeedCacheDocument>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.FeedType)
                        .Ascending(x => x.Page),
                    new CreateIndexOptions { Name = "ix_feed_cache_user_type_page", Background = true }),

                new CreateIndexModel<FeedCacheDocument>(
                    Builders<FeedCacheDocument>.IndexKeys.Ascending(x => x.ExpiresAt),
                    new CreateIndexOptions { Name = "ix_feed_cache_expires", Background = true, ExpireAfter = TimeSpan.Zero })
            });

            // ModelPredictions indexes
            await ModelPredictions.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<ModelPredictionDocument>(
                    Builders<ModelPredictionDocument>.IndexKeys
                        .Ascending(x => x.UserId)
                        .Ascending(x => x.ContentId),
                    new CreateIndexOptions { Name = "ix_predictions_user_content", Background = true }),

                new CreateIndexModel<ModelPredictionDocument>(
                    Builders<ModelPredictionDocument>.IndexKeys
                        .Ascending(x => x.ModelName)
                        .Ascending(x => x.ModelVersion),
                    new CreateIndexOptions { Name = "ix_predictions_model", Background = true }),

                new CreateIndexModel<ModelPredictionDocument>(
                    Builders<ModelPredictionDocument>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_predictions_time", Background = true })
            });

            // AlgorithmMetrics indexes
            await AlgorithmMetrics.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<AlgorithmMetricsDocument>(
                    Builders<AlgorithmMetricsDocument>.IndexKeys
                        .Ascending(x => x.MetricType)
                        .Descending(x => x.Timestamp),
                    new CreateIndexOptions { Name = "ix_metrics_type_time", Background = true }),

                new CreateIndexModel<AlgorithmMetricsDocument>(
                    Builders<AlgorithmMetricsDocument>.IndexKeys.Ascending(x => x.AlgorithmVersion),
                    new CreateIndexOptions { Name = "ix_metrics_version", Background = true })
            });

            // FeedImpressions indexes
            await FeedImpressions.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<FeedImpressionDocument>(
                    Builders<FeedImpressionDocument>.IndexKeys.Ascending(x => x.PostId),
                    new CreateIndexOptions { Name = "ix_impressions_post", Background = true }),

                new CreateIndexModel<FeedImpressionDocument>(
                    Builders<FeedImpressionDocument>.IndexKeys.Ascending(x => x.AlgorithmVersion),
                    new CreateIndexOptions { Name = "ix_impressions_algorithm", Background = true })
            });
        }
    }
}
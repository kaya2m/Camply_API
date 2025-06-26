using Camply.Domain.Messages;
using Camply.Domain.Analytics;
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

        // Database property for LocationAnalyticsService
        public IMongoDatabase Database => _database;

        // Message Collections
        public IMongoCollection<Conversation> Conversations =>
            _database.GetCollection<Conversation>("Conversations");

        public IMongoCollection<Message> Messages =>
            _database.GetCollection<Message>("Messages");

        public IMongoCollection<Reaction> Reactions =>
            _database.GetCollection<Reaction>("Reactions");

        // Analytics Collections
        public IMongoCollection<LocationView> LocationViews =>
            _database.GetCollection<LocationView>("LocationViews");

        public IMongoCollection<LocationInteraction> LocationInteractions =>
            _database.GetCollection<LocationInteraction>("LocationInteractions");

        public IMongoCollection<LocationPopularityMetrics> PopularityMetrics =>
            _database.GetCollection<LocationPopularityMetrics>("LocationPopularityMetrics");

        public IMongoCollection<LocationSearchMetrics> SearchMetrics =>
            _database.GetCollection<LocationSearchMetrics>("LocationSearchMetrics");

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

            await InitializeMessageCollectionsAsync(collectionNames);

            await InitializeAnalyticsCollectionsAsync(collectionNames);

            await InitializeUserIndexesAsync();
        }

        private async Task InitializeMessageCollectionsAsync(List<string> collectionNames)
        {
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
        }

        private async Task InitializeAnalyticsCollectionsAsync(List<string> collectionNames)
        {
            if (!collectionNames.Contains("LocationViews"))
            {
                await _database.CreateCollectionAsync("LocationViews");

                await LocationViews.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys
                            .Ascending(v => v.LocationId)
                            .Ascending(v => v.ViewedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_location_viewdate" }),

                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys
                            .Ascending(v => v.UserId)
                            .Ascending(v => v.ViewedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_user_viewdate" }),

                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys.Ascending(v => v.SessionId),
                        new CreateIndexOptions { Background = true, Name = "ix_session" }),

                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys.Ascending(v => v.ViewedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_viewdate" }),

                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys.Ascending(v => v.IsUniqueView),
                        new CreateIndexOptions { Background = true, Name = "ix_uniqueview" })
                });
            }

            if (!collectionNames.Contains("LocationInteractions"))
            {
                await _database.CreateCollectionAsync("LocationInteractions");

                await LocationInteractions.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<LocationInteraction>(
                        Builders<LocationInteraction>.IndexKeys
                            .Ascending(i => i.LocationId)
                            .Ascending(i => i.CreatedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_location_createdate" }),

                    new CreateIndexModel<LocationInteraction>(
                        Builders<LocationInteraction>.IndexKeys
                            .Ascending(i => i.UserId)
                            .Ascending(i => i.CreatedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_user_createdate" }),

                    new CreateIndexModel<LocationInteraction>(
                        Builders<LocationInteraction>.IndexKeys.Ascending(i => i.InteractionType),
                        new CreateIndexOptions { Background = true, Name = "ix_interactiontype" })
                });
            }

            if (!collectionNames.Contains("LocationPopularityMetrics"))
            {
                await _database.CreateCollectionAsync("LocationPopularityMetrics");

                await PopularityMetrics.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<LocationPopularityMetrics>(
                        Builders<LocationPopularityMetrics>.IndexKeys
                            .Ascending(m => m.LocationId)
                            .Ascending(m => m.Date),
                        new CreateIndexOptions { Background = true, Name = "ix_location_date", Unique = true }),

                    new CreateIndexModel<LocationPopularityMetrics>(
                        Builders<LocationPopularityMetrics>.IndexKeys.Ascending(m => m.Date),
                        new CreateIndexOptions { Background = true, Name = "ix_date" }),

                    new CreateIndexModel<LocationPopularityMetrics>(
                        Builders<LocationPopularityMetrics>.IndexKeys.Descending(m => m.ViewCount),
                        new CreateIndexOptions { Background = true, Name = "ix_viewcount_desc" }),

                    new CreateIndexModel<LocationPopularityMetrics>(
                        Builders<LocationPopularityMetrics>.IndexKeys.Descending(m => m.ConversionRate),
                        new CreateIndexOptions { Background = true, Name = "ix_conversionrate_desc" })
                });
            }

            if (!collectionNames.Contains("LocationSearchMetrics"))
            {
                await _database.CreateCollectionAsync("LocationSearchMetrics");

                await SearchMetrics.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys.Ascending(s => s.SearchTerm),
                        new CreateIndexOptions { Background = true, Name = "ix_searchterm" }),

                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys.Ascending(s => s.SearchedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_searchdate" }),

                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys
                            .Ascending(s => s.UserId)
                            .Ascending(s => s.SearchedAt),
                        new CreateIndexOptions { Background = true, Name = "ix_user_searchdate" }),

                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys.Ascending(s => s.ResultCount),
                        new CreateIndexOptions { Background = true, Name = "ix_resultcount" })
                });
            }
        }

        private async Task InitializeUserIndexesAsync()
        {
            try
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
            catch (Exception)
            {
                // Users collection might not exist yet, which is fine
            }
        }

        // Utility methods for analytics
        public async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var collectionNames = await (await _database.ListCollectionNamesAsync()).ToListAsync();
            return collectionNames.Contains(collectionName);
        }

        public async Task DropCollectionAsync(string collectionName)
        {
            await _database.DropCollectionAsync(collectionName);
        }

        public async Task<long> GetCollectionCountAsync(string collectionName)
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            return await collection.CountDocumentsAsync(new BsonDocument());
        }

        // Health check for all collections
        public async Task<Dictionary<string, bool>> CheckAllCollectionsHealthAsync()
        {
            var results = new Dictionary<string, bool>();
            var collectionNames = new[] {
                "Conversations", "Messages", "Reactions",
                "LocationViews", "LocationInteractions",
                "LocationPopularityMetrics", "LocationSearchMetrics"
            };

            foreach (var collectionName in collectionNames)
            {
                try
                {
                    var collection = _database.GetCollection<BsonDocument>(collectionName);
                    await collection.CountDocumentsAsync(new BsonDocument(),
                        new CountOptions { Limit = 1 });
                    results[collectionName] = true;
                }
                catch
                {
                    results[collectionName] = false;
                }
            }

            return results;
        }
    }
}
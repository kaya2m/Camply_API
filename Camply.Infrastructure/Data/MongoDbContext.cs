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
        }
    }
}
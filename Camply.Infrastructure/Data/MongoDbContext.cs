using Camply.Domain.Messages;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Options;
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
    }
}
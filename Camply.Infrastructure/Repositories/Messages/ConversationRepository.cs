using Camply.Application.Messages.Interfaces;
using Camply.Domain.Messages;
using Camply.Infrastructure.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Repositories.Messages
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly MongoDbContext _context;

        public ConversationRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId, int skip = 0, int limit = 20)
        {
            var filter = Builders<Conversation>.Filter.AnyEq(c => c.ParticipantIds, userId) &
                         Builders<Conversation>.Filter.Ne(c => c.Status, "deleted");

            var sort = Builders<Conversation>.Sort.Descending(c => c.LastActivityDate);

            return await _context.Conversations
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<Conversation> GetConversationByIdAsync(string id)
        {
            return await _context.Conversations
                .Find(c => c.Id == id && c.Status != "deleted")
                .FirstOrDefaultAsync();
        }

        public async Task<Conversation> CreateConversationAsync(Conversation conversation)
        {
            await _context.Conversations.InsertOneAsync(conversation);
            return conversation;
        }

        public async Task UpdateConversationAsync(string id, Conversation conversation)
        {
            await _context.Conversations.ReplaceOneAsync(c => c.Id == id, conversation);
        }

        public async Task<Conversation> GetOrCreateOneToOneConversationAsync(string userId1, string userId2)
        {
            // İki kullanıcı arasında var olan birebir konuşmayı bul
            var filter = Builders<Conversation>.Filter.And(
                Builders<Conversation>.Filter.All(c => c.ParticipantIds, new[] { userId1, userId2 }),
                Builders<Conversation>.Filter.Size(c => c.ParticipantIds, 2), // Tam olarak 2 katılımcı
                Builders<Conversation>.Filter.Eq(c => c.IsGroup, false)
            );

            var conversation = await _context.Conversations.Find(filter).FirstOrDefaultAsync();

            if (conversation != null)
            {
                return conversation;
            }

            // Yoksa yeni konuşma oluştur
            var newConversation = new Conversation
            {
                ParticipantIds = new List<string> { userId1, userId2 },
                IsGroup = false,
                CreatedAt = DateTime.UtcNow,
                LastActivityDate = DateTime.UtcNow,
                Status = "active"
            };

            await _context.Conversations.InsertOneAsync(newConversation);
            return newConversation;
        }

        public async Task<long> GetUserConversationsCountAsync(string userId)
        {
            var filter = Builders<Conversation>.Filter.AnyEq(c => c.ParticipantIds, userId) &
                         Builders<Conversation>.Filter.Ne(c => c.Status, "deleted");

            return await _context.Conversations.CountDocumentsAsync(filter);
        }

        public async Task MuteConversationAsync(string conversationId, string userId, bool mute)
        {
            var update = Builders<Conversation>.Update
                .Set($"MutedBy.{userId}", mute);

            await _context.Conversations.UpdateOneAsync(
                c => c.Id == conversationId,
                update);
        }

        public async Task ArchiveConversationAsync(string conversationId, string userId)
        {
            // Arşivleme işlemi için konuşma durumunu güncelle
            var update = Builders<Conversation>.Update
                .Set(c => c.Status, "archived");

            await _context.Conversations.UpdateOneAsync(
                c => c.Id == conversationId,
                update);
        }

        public async Task DeleteConversationAsync(string conversationId, string userId)
        {
            // Soft delete - durumu "deleted" olarak işaretle
            var update = Builders<Conversation>.Update
                .Set(c => c.Status, "deleted");

            await _context.Conversations.UpdateOneAsync(
                c => c.Id == conversationId,
                update);
        }

        public async Task UpdateUnreadCountAsync(string conversationId, string userId, int count)
        {
            var update = Builders<Conversation>.Update
                .Set($"UnreadCount.{userId}", count);

            await _context.Conversations.UpdateOneAsync(
                c => c.Id == conversationId,
                update);
        }
    }
}
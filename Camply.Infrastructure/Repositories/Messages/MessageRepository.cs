using Camply.Application.Messages.Interfaces;
using Camply.Domain.Messages;
using Camply.Infrastructure.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Repositories.Messages
{
    public class MessageRepository : IMessageRepository
    {
        private readonly MongoDbContext _context;

        public MessageRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Message>> GetConversationMessagesAsync(string conversationId, int skip = 0, int limit = 50)
        {
            var filter = Builders<Message>.Filter.Eq(m => m.ConversationId, conversationId) &
                         Builders<Message>.Filter.Eq(m => m.IsDeleted, false);

            var sort = Builders<Message>.Sort.Ascending(m => m.CreatedAt);

            return await _context.Messages
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<Message> GetMessageByIdAsync(string id)
        {
            return await _context.Messages
                .Find(m => m.Id == id && m.IsDeleted == false)
                .FirstOrDefaultAsync();
        }

        public async Task<Message> CreateMessageAsync(Message message)
        {
            await _context.Messages.InsertOneAsync(message);

            // İlgili konuşmayı güncelle
            var update = Builders<Conversation>.Update
                .Set(c => c.LastMessageId, message.Id)
                .Set(c => c.LastMessagePreview, TruncateMessagePreview(message.Content))
                .Set(c => c.LastMessageSenderId, message.SenderId)
                .Set(c => c.LastActivityDate, DateTime.UtcNow);

            await _context.Conversations.UpdateOneAsync(
                c => c.Id == message.ConversationId,
                update);

            // Konuşmaya katılan diğer kullanıcıların okunmamış mesaj sayısını artır
            var conversation = await _context.Conversations
                .Find(c => c.Id == message.ConversationId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                foreach (var participantId in conversation.ParticipantIds)
                {
                    if (participantId != message.SenderId) // Gönderen hariç
                    {
                        var unreadCountUpdate = Builders<Conversation>.Update
                            .Inc($"UnreadCount.{participantId}", 1);

                        await _context.Conversations.UpdateOneAsync(
                            c => c.Id == message.ConversationId,
                            unreadCountUpdate);
                    }
                }
            }

            return message;
        }

        private string TruncateMessagePreview(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            return content.Length <= 50 ? content : content.Substring(0, 47) + "...";
        }

        public async Task UpdateMessageAsync(string id, Message message)
        {
            await _context.Messages.ReplaceOneAsync(m => m.Id == id, message);
        }

        public async Task MarkAsReadAsync(string messageId, string userId)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message == null) return;

            // Eğer bu kullanıcı mesajı zaten okuduysa bir şey yapma
            if (message.ReadBy.ContainsKey(userId)) return;

            // ReadBy sözlüğüne kullanıcı ve okuma zamanını ekle
            var update = Builders<Message>.Update
                .Set($"ReadBy.{userId}", DateTime.UtcNow);

            await _context.Messages.UpdateOneAsync(
                m => m.Id == messageId,
                update);
        }

        public async Task<long> GetUnreadMessagesCountAsync(string userId, string conversationId = null)
        {
            var builder = Builders<Message>.Filter;
            var filter = builder.Ne(m => m.SenderId, userId) &
                         builder.Not(builder.Exists($"ReadBy.{userId}")) &
                         builder.Eq(m => m.IsDeleted, false);

            if (!string.IsNullOrEmpty(conversationId))
            {
                filter &= builder.Eq(m => m.ConversationId, conversationId);
            }
            else
            {
                var conversationFilter = Builders<Conversation>.Filter.AnyEq(c => c.ParticipantIds, userId);
                var conversationIds = await _context.Conversations
                    .Find(conversationFilter)
                    .Project(c => c.Id)
                    .ToListAsync();

                filter &= builder.In(m => m.ConversationId, conversationIds);
            }

            return await _context.Messages.CountDocumentsAsync(filter);
        }

        public async Task ToggleLikeMessageAsync(string messageId, string userId)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message == null) return;

            UpdateDefinition<Message> update;

            // Kullanıcı mesajı beğenmişse beğeniyi kaldır, aksi halde ekle
            if (message.LikedBy.Contains(userId))
            {
                update = Builders<Message>.Update.Pull(m => m.LikedBy, userId);
            }
            else
            {
                update = Builders<Message>.Update.Push(m => m.LikedBy, userId);
            }

            await _context.Messages.UpdateOneAsync(m => m.Id == messageId, update);
        }

        public async Task ToggleSaveMessageAsync(string messageId, string userId)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message == null) return;

            // Mesajı gönderen veya mesajın ait olduğu konuşmada yer alan kullanıcılar kaydedebilir
            var conversation = await _context.Conversations
                .Find(c => c.Id == message.ConversationId)
                .FirstOrDefaultAsync();

            if (conversation == null || !conversation.ParticipantIds.Contains(userId)) return;

            var update = Builders<Message>.Update
                .Set(m => m.IsSaved, !message.IsSaved);

            await _context.Messages.UpdateOneAsync(m => m.Id == messageId, update);
        }

        public async Task DeleteMessageAsync(string messageId, string userId)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message == null) return;

            // Sadece mesajı gönderen kişi silebilir
            if (message.SenderId != userId) return;

            var update = Builders<Message>.Update
                .Set(m => m.IsDeleted, true);

            await _context.Messages.UpdateOneAsync(m => m.Id == messageId, update);

            // Eğer silinen mesaj konuşmanın son mesajı ise, konuşmanın son mesajını güncelle
            var conversation = await _context.Conversations
                .Find(c => c.Id == message.ConversationId && c.LastMessageId == messageId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                // Bir önceki mesajı bul
                var previousMessage = await _context.Messages
                    .Find(m => m.ConversationId == message.ConversationId &&
                           m.IsDeleted == false &&
                           m.CreatedAt < message.CreatedAt)
                    .Sort(Builders<Message>.Sort.Descending(m => m.CreatedAt))
                    .FirstOrDefaultAsync();

                if (previousMessage != null)
                {
                    var conversationUpdate = Builders<Conversation>.Update
                        .Set(c => c.LastMessageId, previousMessage.Id)
                        .Set(c => c.LastMessagePreview, TruncateMessagePreview(previousMessage.Content))
                        .Set(c => c.LastMessageSenderId, previousMessage.SenderId);

                    await _context.Conversations.UpdateOneAsync(c => c.Id == conversation.Id, conversationUpdate);
                }
            }
        }

        public async Task EditMessageAsync(string messageId, string newContent)
        {
            var message = await GetMessageByIdAsync(messageId);
            if (message == null) return;

            var update = Builders<Message>.Update
                .Set(m => m.Content, newContent)
                .Set(m => m.IsEdited, true)
                .Set(m => m.EditedAt, DateTime.UtcNow);

            await _context.Messages.UpdateOneAsync(m => m.Id == messageId, update);

            // Eğer düzenlenen mesaj konuşmanın son mesajı ise, önizlemeyi güncelle
            var conversation = await _context.Conversations
                .Find(c => c.Id == message.ConversationId && c.LastMessageId == messageId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                var conversationUpdate = Builders<Conversation>.Update
                    .Set(c => c.LastMessagePreview, TruncateMessagePreview(newContent));

                await _context.Conversations.UpdateOneAsync(c => c.Id == conversation.Id, conversationUpdate);
            }
        }

        public async Task<IEnumerable<Message>> GetMediaMessagesAsync(string conversationId, int skip = 0, int limit = 20)
        {
            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.ConversationId, conversationId),
                Builders<Message>.Filter.Eq(m => m.IsDeleted, false),
                Builders<Message>.Filter.Or(
                    Builders<Message>.Filter.Eq(m => m.MessageType, "image"),
                    Builders<Message>.Filter.Eq(m => m.MessageType, "video"),
                    Builders<Message>.Filter.Eq(m => m.MessageType, "audio"),
                    Builders<Message>.Filter.Eq(m => m.MessageType, "file")
                )
            );

            var sort = Builders<Message>.Sort.Descending(m => m.CreatedAt);

            return await _context.Messages
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<IEnumerable<Message>> SearchMessagesAsync(string conversationId, string query, int skip = 0, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Message>();

            var filter = Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.ConversationId, conversationId),
                Builders<Message>.Filter.Eq(m => m.IsDeleted, false),
                Builders<Message>.Filter.Regex(m => m.Content, new MongoDB.Bson.BsonRegularExpression(query, "i"))
            );

            var sort = Builders<Message>.Sort.Descending(m => m.CreatedAt);

            return await _context.Messages
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }
    }
}
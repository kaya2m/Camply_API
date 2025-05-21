using Camply.Application.Messages.Interfaces;
using Camply.Domain.Messages;
using Camply.Infrastructure.Data;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Repositories.Messages
{
    public class ReactionRepository : IReactionRepository
    {
        private readonly MongoDbContext _context;

        public ReactionRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Reaction>> GetMessageReactionsAsync(string messageId)
        {
            return await _context.Reactions
                .Find(r => r.MessageId == messageId)
                .ToListAsync();
        }

        public async Task<Reaction> AddReactionAsync(Reaction reaction)
        {
            // Önceden kullanıcının bu mesaja tepki verip vermediğini kontrol et
            var existingReaction = await _context.Reactions
                .Find(r => r.MessageId == reaction.MessageId && r.UserId == reaction.UserId)
                .FirstOrDefaultAsync();

            if (existingReaction != null)
            {
                // Varolan tepkiyi güncelle
                await UpdateReactionAsync(reaction.MessageId, reaction.UserId, reaction.ReactionType);
                return existingReaction;
            }

            // Yeni tepki ekle
            await _context.Reactions.InsertOneAsync(reaction);
            return reaction;
        }

        public async Task RemoveReactionAsync(string messageId, string userId)
        {
            await _context.Reactions.DeleteOneAsync(r => r.MessageId == messageId && r.UserId == userId);
        }

        public async Task<Reaction> GetUserReactionAsync(string messageId, string userId)
        {
            return await _context.Reactions
                .Find(r => r.MessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateReactionAsync(string messageId, string userId, string newReactionType)
        {
            var update = Builders<Reaction>.Update
                .Set(r => r.ReactionType, newReactionType);

            await _context.Reactions.UpdateOneAsync(
                r => r.MessageId == messageId && r.UserId == userId,
                update);
        }
    }
}
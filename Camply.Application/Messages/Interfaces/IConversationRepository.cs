using Camply.Domain.Messages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces
{
    public interface IConversationRepository
    {
        Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId, int skip = 0, int limit = 20);

        Task<Conversation> GetConversationByIdAsync(string id);

        Task<Conversation> CreateConversationAsync(Conversation conversation);

        Task UpdateConversationAsync(string id, Conversation conversation);

        Task<Conversation> GetOrCreateOneToOneConversationAsync(string userId1, string userId2);

        Task<long> GetUserConversationsCountAsync(string userId);

        Task MuteConversationAsync(string conversationId, string userId, bool mute);

        Task ArchiveConversationAsync(string conversationId, string userId);

        Task DeleteConversationAsync(string conversationId, string userId);

        Task UpdateUnreadCountAsync(string conversationId, string userId, int count);
    }
}
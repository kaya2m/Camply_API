using Camply.Application.Messages.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces.Services
{
    public interface IConversationService
    {
        Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId, int page = 1, int pageSize = 20);
        Task<ConversationDto> GetConversationByIdAsync(string id, string userId);
        Task<ConversationDto> CreateConversationAsync(CreateConversationDto createConversationDto, string creatorId);
        Task<ConversationDto> GetOrCreateDirectConversationAsync(string currentUserId, string otherUserId);
        Task MuteConversationAsync(string conversationId, string userId, bool mute);
        Task ArchiveConversationAsync(string conversationId, string userId);
        Task DeleteConversationAsync(string conversationId, string userId);
    }
}

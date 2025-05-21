using Camply.Application.Messages.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces.Services
{
    public interface IMessageService
    {
        Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(string conversationId, string userId, int page = 1, int pageSize = 50);
        Task<MessageDto> GetMessageByIdAsync(string id, string userId);
        Task<MessageDto> SendMessageAsync(SendMessageDto sendMessageDto, string senderId);
        Task MarkAsReadAsync(string messageId, string userId);
        Task MarkConversationAsReadAsync(string conversationId, string userId);
        Task<MessageDto> EditMessageAsync(string messageId, string userId, string newContent);
        Task DeleteMessageAsync(string messageId, string userId);
        Task<MessageDto> ToggleLikeMessageAsync(string messageId, string userId);
        Task<MessageDto> ToggleSaveMessageAsync(string messageId, string userId);
        Task<IEnumerable<MessageDto>> GetMediaMessagesAsync(string conversationId, string userId, int page = 1, int pageSize = 20);
        Task<IEnumerable<MessageDto>> SearchMessagesAsync(string conversationId, string userId, string query, int page = 1, int pageSize = 20);
    }
}

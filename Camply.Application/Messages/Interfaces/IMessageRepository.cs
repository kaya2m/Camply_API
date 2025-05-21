using Camply.Domain.Messages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces
{
    public interface IMessageRepository
    {
        // Konuşmaya ait mesajları getir
        Task<IEnumerable<Message>> GetConversationMessagesAsync(string conversationId, int skip = 0, int limit = 50);

        // ID'ye göre mesaj getir
        Task<Message> GetMessageByIdAsync(string id);

        // Yeni mesaj oluştur
        Task<Message> CreateMessageAsync(Message message);

        // Mesajı güncelle
        Task UpdateMessageAsync(string id, Message message);

        // Mesajı okundu olarak işaretle
        Task MarkAsReadAsync(string messageId, string userId);

        // Okunmamış mesaj sayısını getir
        Task<long> GetUnreadMessagesCountAsync(string userId, string conversationId = null);

        // Mesajı beğen/beğenme
        Task ToggleLikeMessageAsync(string messageId, string userId);

        // Mesajı kaydet/kaydetme
        Task ToggleSaveMessageAsync(string messageId, string userId);

        // Mesajı sil (soft delete)
        Task DeleteMessageAsync(string messageId, string userId);

        // Mesajı düzenle
        Task EditMessageAsync(string messageId, string newContent);

        // Medya mesajları getir
        Task<IEnumerable<Message>> GetMediaMessagesAsync(string conversationId, int skip = 0, int limit = 20);

        // Arama yapma 
        Task<IEnumerable<Message>> SearchMessagesAsync(string conversationId, string query, int skip = 0, int limit = 20);
    }
}
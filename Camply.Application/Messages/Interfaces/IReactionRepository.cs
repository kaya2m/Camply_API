using Camply.Domain.Messages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces
{
    public interface IReactionRepository
    {
        // Mesaja ait tüm tepkileri getir
        Task<IEnumerable<Reaction>> GetMessageReactionsAsync(string messageId);

        // Tepki ekle
        Task<Reaction> AddReactionAsync(Reaction reaction);

        // Tepkiyi kaldır
        Task RemoveReactionAsync(string messageId, string userId);

        // Kullanıcının tepkisini getir
        Task<Reaction> GetUserReactionAsync(string messageId, string userId);

        // Tepkiyi güncelle (emoji değiştirme)
        Task UpdateReactionAsync(string messageId, string userId, string newReactionType);
    }
}
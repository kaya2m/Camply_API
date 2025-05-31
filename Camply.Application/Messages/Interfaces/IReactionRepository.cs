using Camply.Domain.Messages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces
{
    public interface IReactionRepository
    {
        Task<IEnumerable<Reaction>> GetMessageReactionsAsync(string messageId);

        Task<Reaction> AddReactionAsync(Reaction reaction);

        Task RemoveReactionAsync(string messageId, string userId);

        Task<Reaction> GetUserReactionAsync(string messageId, string userId);

        Task UpdateReactionAsync(string messageId, string userId, string newReactionType);
    }
}
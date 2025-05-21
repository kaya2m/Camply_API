using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Interfaces.Services
{
    public interface IReactionService
    {
        Task<IEnumerable<ReactionDto>> GetMessageReactionsAsync(string messageId);
        Task<ReactionDto> AddReactionAsync(AddReactionDto addReactionDto, string userId);
        Task RemoveReactionAsync(string messageId, string userId);
    }
}

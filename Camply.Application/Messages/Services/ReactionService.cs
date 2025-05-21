using Camply.Application.Messages.DTOs;
using Camply.Application.Messages.Interfaces;
using Camply.Application.Messages.Interfaces.Services;
using Camply.Application.Users.Interfaces;
using Camply.Domain.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Services
{
    public class ReactionService : IReactionService
    {
        private readonly IReactionRepository _reactionRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IUserService _userService;
        private readonly ILogger<ReactionService> _logger;

        public ReactionService(
            IReactionRepository reactionRepository,
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IUserService userService,
            ILogger<ReactionService> logger)
        {
            _reactionRepository = reactionRepository;
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _userService = userService;
            _logger = logger;
        }

        public async Task<IEnumerable<ReactionDto>> GetMessageReactionsAsync(string messageId)
        {
            try
            {
                var reactions = await _reactionRepository.GetMessageReactionsAsync(messageId);
                return await MapReactionsToDtosAsync(reactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting reactions for message {messageId}");
                throw;
            }
        }

        public async Task<ReactionDto> AddReactionAsync(AddReactionDto addReactionDto, string userId)
        {
            try
            {
                var message = await _messageRepository.GetMessageByIdAsync(addReactionDto.MessageId);
                if (message == null)
                {
                    throw new KeyNotFoundException($"Message with ID {addReactionDto.MessageId} not found");
                }

                // Kullanıcının bu mesaja erişim yetkisi var mı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(message.ConversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access message {addReactionDto.MessageId}");
                }

                // Mevcut tepki varsa güncelle, yoksa ekle
                var existingReaction = await _reactionRepository.GetUserReactionAsync(addReactionDto.MessageId, userId);
                if (existingReaction != null)
                {
                    // Aynı tepki tekrar verilirse kaldır
                    if (existingReaction.ReactionType == addReactionDto.ReactionType)
                    {
                        await _reactionRepository.RemoveReactionAsync(addReactionDto.MessageId, userId);
                        return null;
                    }
                    else
                    {
                        // Farklı tepki verilirse güncelle
                        await _reactionRepository.UpdateReactionAsync(addReactionDto.MessageId, userId, addReactionDto.ReactionType);
                        var updatedReaction = await _reactionRepository.GetUserReactionAsync(addReactionDto.MessageId, userId);
                        var result = await MapReactionsToDtosAsync(new List<Reaction> { updatedReaction });
                        return result.FirstOrDefault();
                    }
                }
                else
                {
                    // Yeni tepki ekle
                    var reaction = new Reaction
                    {
                        MessageId = addReactionDto.MessageId,
                        UserId = userId,
                        ReactionType = addReactionDto.ReactionType,
                        CreatedAt = DateTime.UtcNow
                    };

                    var addedReaction = await _reactionRepository.AddReactionAsync(reaction);
                    var result = await MapReactionsToDtosAsync(new List<Reaction> { addedReaction });
                    return result.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding reaction to message {addReactionDto.MessageId}");
                throw;
            }
        }

        public async Task RemoveReactionAsync(string messageId, string userId)
        {
            try
            {
                var message = await _messageRepository.GetMessageByIdAsync(messageId);
                if (message == null)
                {
                    throw new KeyNotFoundException($"Message with ID {messageId} not found");
                }

                // Kullanıcının bu mesaja erişim yetkisi var mı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(message.ConversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access message {messageId}");
                }

                await _reactionRepository.RemoveReactionAsync(messageId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing reaction from message {messageId}");
                throw;
            }
        }

        #region Helper Methods

        private async Task<IEnumerable<ReactionDto>> MapReactionsToDtosAsync(IEnumerable<Reaction> reactions)
        {
            var result = new List<ReactionDto>();

            foreach (var reaction in reactions)
            {
                var user = await _userService.GetUserMinimalAsync(reaction.UserId);
                if (user != null)
                {
                    result.Add(new ReactionDto
                    {
                        Id = reaction.Id,
                        MessageId = reaction.MessageId,
                        User = user,
                        ReactionType = reaction.ReactionType,
                        CreatedAt = reaction.CreatedAt
                    });
                }
            }

            return result;
        }

        #endregion
    }
}
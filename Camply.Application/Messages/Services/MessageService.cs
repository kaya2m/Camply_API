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
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IReactionRepository _reactionRepository;
        private readonly IUserService _userService;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IReactionRepository reactionRepository,
            IUserService userService,
            ILogger<MessageService> logger)
        {
            _messageRepository = messageRepository;
            _conversationRepository = conversationRepository;
            _reactionRepository = reactionRepository;
            _userService = userService;
            _logger = logger;
        }

        public async Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(string conversationId, string userId, int page = 1, int pageSize = 50)
        {
            try
            {
                int skip = (page - 1) * pageSize;

                // Kullanıcının konuşmaya yetkisi var mı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access conversation {conversationId}");
                }

                var messages = await _messageRepository.GetConversationMessagesAsync(conversationId, skip, pageSize);
                return await MapMessagesToDtosAsync(messages, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting messages for conversation {conversationId}");
                throw;
            }
        }

        public async Task<MessageDto> GetMessageByIdAsync(string id, string userId)
        {
            try
            {
                var message = await _messageRepository.GetMessageByIdAsync(id);
                if (message == null)
                {
                    return null;
                }

                // Kullanıcının bu mesaja erişim yetkisi var mı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(message.ConversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access message {id}");
                }

                var messageDtos = await MapMessagesToDtosAsync(new List<Message> { message }, userId);
                return messageDtos.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting message with ID {id}");
                throw;
            }
        }

        public async Task<MessageDto> SendMessageAsync(SendMessageDto sendMessageDto, string senderId)
        {
            try
            {
                // Konuşmayı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(sendMessageDto.ConversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(senderId))
                {
                    throw new UnauthorizedAccessException($"User {senderId} is not authorized to send message to conversation {sendMessageDto.ConversationId}");
                }

                string replyToMessageId = string.IsNullOrWhiteSpace(sendMessageDto.ReplyToMessageId)
             ? null
             : sendMessageDto.ReplyToMessageId;

                if (!string.IsNullOrEmpty(replyToMessageId))
                {
                    var replyMessage = await _messageRepository.GetMessageByIdAsync(replyToMessageId);
                    if (replyMessage == null || replyMessage.ConversationId != sendMessageDto.ConversationId)
                    {
                        throw new InvalidOperationException("Invalid reply message ID");
                    }
                }

                var message = new Message
                {
                    ConversationId = sendMessageDto.ConversationId,
                    SenderId = senderId,
                    Content = sendMessageDto.Content,
                    MessageType = sendMessageDto.MessageType,
                    ReplyToMessageId = replyToMessageId,
                    CreatedAt = DateTime.UtcNow,
                    ReadBy = new Dictionary<string, DateTime> { { senderId, DateTime.UtcNow } },
                    Media = sendMessageDto.Media?.Select(m => new MediaAttachment
                    {
                        MediaType = m.MediaType,
                        Url = m.Url,
                        ThumbnailUrl = m.ThumbnailUrl,
                        FileName = m.FileName,
                        FileSize = m.FileSize,
                        Width = m.Width,
                        Height = m.Height,
                        Duration = m.Duration
                    }).ToList() ?? new List<MediaAttachment>()
                };

                var createdMessage = await _messageRepository.CreateMessageAsync(message);

                foreach (var participantId in conversation.ParticipantIds)
                {
                    if (participantId != senderId)
                    {
                        var unreadCount = await _messageRepository.GetUnreadMessagesCountAsync(participantId, conversation.Id);
                        await _conversationRepository.UpdateUnreadCountAsync(conversation.Id, participantId, (int)unreadCount);
                    }
                }

                var messageDtos = await MapMessagesToDtosAsync(new List<Message> { createdMessage }, senderId);
                return messageDtos.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                throw;
            }
        }

        public async Task MarkAsReadAsync(string messageId, string userId)
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

                await _messageRepository.MarkAsReadAsync(messageId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking message {messageId} as read");
                throw;
            }
        }

        public async Task MarkConversationAsReadAsync(string conversationId, string userId)
        {
            try
            {
                var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access conversation {conversationId}");
                }

                var unreadMessages = (await _messageRepository.GetConversationMessagesAsync(conversationId))
                    .Where(m => m.SenderId != userId && !m.ReadBy.ContainsKey(userId))
                    .ToList();

                foreach (var message in unreadMessages)
                {
                    await _messageRepository.MarkAsReadAsync(message.Id, userId);
                }

                // Okunmamış sayıyı sıfırla
                await _conversationRepository.UpdateUnreadCountAsync(conversationId, userId, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking conversation {conversationId} as read");
                throw;
            }
        }

        public async Task<MessageDto> EditMessageAsync(string messageId, string userId, string newContent)
        {
            try
            {
                var message = await _messageRepository.GetMessageByIdAsync(messageId);
                if (message == null)
                {
                    throw new KeyNotFoundException($"Message with ID {messageId} not found");
                }

                if (message.SenderId != userId)
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to edit message {messageId}");
                }

                await _messageRepository.EditMessageAsync(messageId, newContent);

                var updatedMessage = await _messageRepository.GetMessageByIdAsync(messageId);
                var messageDtos = await MapMessagesToDtosAsync(new List<Message> { updatedMessage }, userId);
                return messageDtos.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error editing message {messageId}");
                throw;
            }
        }

        public async Task DeleteMessageAsync(string messageId, string userId)
        {
            try
            {
                var message = await _messageRepository.GetMessageByIdAsync(messageId);
                if (message == null)
                {
                    throw new KeyNotFoundException($"Message with ID {messageId} not found");
                }

                // Sadece mesajı gönderen kişi silebilir
                if (message.SenderId != userId)
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to delete message {messageId}");
                }

                await _messageRepository.DeleteMessageAsync(messageId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting message {messageId}");
                throw;
            }
        }

        public async Task<MessageDto> ToggleLikeMessageAsync(string messageId, string userId)
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

                await _messageRepository.ToggleLikeMessageAsync(messageId, userId);

                // Güncellenmiş mesajı al
                var updatedMessage = await _messageRepository.GetMessageByIdAsync(messageId);
                var messageDtos = await MapMessagesToDtosAsync(new List<Message> { updatedMessage }, userId);
                return messageDtos.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling like for message {messageId}");
                throw;
            }
        }

        public async Task<MessageDto> ToggleSaveMessageAsync(string messageId, string userId)
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

                await _messageRepository.ToggleSaveMessageAsync(messageId, userId);

                // Güncellenmiş mesajı al
                var updatedMessage = await _messageRepository.GetMessageByIdAsync(messageId);
                var messageDtos = await MapMessagesToDtosAsync(new List<Message> { updatedMessage }, userId);
                return messageDtos.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling save for message {messageId}");
                throw;
            }
        }

        public async Task<IEnumerable<MessageDto>> GetMediaMessagesAsync(string conversationId, string userId, int page = 1, int pageSize = 20)
        {
            try
            {
                int skip = (page - 1) * pageSize;

                // Konuşmayı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access conversation {conversationId}");
                }

                var messages = await _messageRepository.GetMediaMessagesAsync(conversationId, skip, pageSize);
                return await MapMessagesToDtosAsync(messages, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media messages for conversation {conversationId}");
                throw;
            }
        }

        public async Task<IEnumerable<MessageDto>> SearchMessagesAsync(string conversationId, string userId, string query, int page = 1, int pageSize = 20)
        {
            try
            {
                int skip = (page - 1) * pageSize;

                // Konuşmayı kontrol et
                var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
                if (conversation == null || !conversation.ParticipantIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException($"User {userId} is not authorized to access conversation {conversationId}");
                }

                var messages = await _messageRepository.SearchMessagesAsync(conversationId, query, skip, pageSize);
                return await MapMessagesToDtosAsync(messages, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching messages in conversation {conversationId}");
                throw;
            }
        }

        #region Helper Methods

        private async Task<IEnumerable<MessageDto>> MapMessagesToDtosAsync(IEnumerable<Message> messages, string currentUserId)
        {
            var result = new List<MessageDto>();

            foreach (var message in messages)
            {
                var sender = await _userService.GetUserMinimalAsync(message.SenderId);

                // Yanıtlanan mesaj bilgilerini al
                MessageReplyDto replyTo = null;
                if (!string.IsNullOrEmpty(message.ReplyToMessageId))
                {
                    var replyMessage = await _messageRepository.GetMessageByIdAsync(message.ReplyToMessageId);
                    if (replyMessage != null)
                    {
                        var replySender = await _userService.GetUserMinimalAsync(replyMessage.SenderId);
                        replyTo = new MessageReplyDto
                        {
                            MessageId = replyMessage.Id,
                            Content = replyMessage.Content,
                            Sender = replySender
                        };
                    }
                }

                // Okundu bilgilerini al
                var readByUsers = new List<UserReadDto>();
                foreach (var read in message.ReadBy)
                {
                    var user = await _userService.GetUserMinimalAsync(read.Key);
                    if (user != null)
                    {
                        readByUsers.Add(new UserReadDto
                        {
                            UserId = read.Key,
                            Username = user.Username,
                            ProfilePictureUrl = user.ProfilePictureUrl,
                            ReadAt = read.Value
                        });
                    }
                }

                // Tepkileri al
                var reactions = await _reactionRepository.GetMessageReactionsAsync(message.Id);
                var reactionDtos = new List<ReactionDto>();

                foreach (var reaction in reactions)
                {
                    var user = await _userService.GetUserMinimalAsync(reaction.UserId);
                    if (user != null)
                    {
                        reactionDtos.Add(new ReactionDto
                        {
                            Id = reaction.Id,
                            MessageId = reaction.MessageId,
                            User = user,
                            ReactionType = reaction.ReactionType,
                            CreatedAt = reaction.CreatedAt
                        });
                    }
                }

                result.Add(new MessageDto
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    Sender = sender,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    ReplyTo = replyTo,
                    Media = message.Media?.Select(m => new MediaAttachmentDto
                    {
                        MediaType = m.MediaType,
                        Url = m.Url,
                        ThumbnailUrl = m.ThumbnailUrl,
                        FileName = m.FileName,
                        FileSize = m.FileSize,
                        Width = m.Width,
                        Height = m.Height,
                        Duration = m.Duration
                    }).ToList(),
                    IsRead = message.ReadBy.ContainsKey(currentUserId),
                    ReadBy = readByUsers,
                    CreatedAt = message.CreatedAt,
                    IsEdited = message.IsEdited,
                    EditedAt = message.EditedAt,
                    IsSaved = message.IsSaved,
                    Reactions = reactionDtos
                });
            }

            return result;
        }

        #endregion
    }
}
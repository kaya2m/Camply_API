using Camply.Application.Messages.DTOs;
using Camply.Application.Messages.Interfaces.Services;
using Camply.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Camply.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IConversationService _conversationService;
        private readonly ILogger<ChatHub> _logger;
        private readonly UserPresenceTracker _presenceTracker;
        public ChatHub(
            IMessageService messageService,
            IConversationService conversationService,
            ILogger<ChatHub> logger,
            UserPresenceTracker presenceTracker)
        {
            _messageService = messageService;
            _conversationService = conversationService;
            _logger = logger;
            _presenceTracker = presenceTracker;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = GetUserId();
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                    await _presenceTracker.UserConnected(userId);

                    await Clients.Others.SendAsync("UserOnline", userId);
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var userId = GetUserId();
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

                    await _presenceTracker.UserDisconnected(userId);

                    _logger.LogInformation($"User {userId} disconnected from chat hub");

                    await Clients.Others.SendAsync("UserOffline", userId);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
                throw;
            }
        }
        public async Task JoinConversation(string conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Kullanıcının konuşmaya erişimi var mı 
                var conversation = await _conversationService.GetConversationByIdAsync(conversationId, userId);
                if (conversation == null)
                {
                    throw new UnauthorizedAccessException($"User {userId} does not have access to conversation {conversationId}");
                }

                // Kullanıcıyı konuşma grubuna ekle
                await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

                _logger.LogInformation($"User {userId} joined conversation {conversationId}");

                // Konuşmanın diğer katılımcılarına kullanıcının katıldığını bildir
                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("UserJoinedConversation", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error joining conversation {conversationId}");
                throw;
            }
        }

        public async Task LeaveConversation(string conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Kullanıcıyı konuşma grubundan çıkar
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

                _logger.LogInformation($"User {userId} left conversation {conversationId}");

                // Konuşmanın diğer katılımcılarına kullanıcının ayrıldığını bildir
                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("UserLeftConversation", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error leaving conversation {conversationId}");
                throw;
            }
        }

        public async Task SendMessage(SendMessageDto messageDto)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Mesajı veritabanına kaydet
                var message = await _messageService.SendMessageAsync(messageDto, userId);

                // Mesajı konuşma grubuna gönder
                await Clients.Group($"conversation_{messageDto.ConversationId}")
                    .SendAsync("ReceiveMessage", message);

                var conversation = await _conversationService.GetConversationByIdAsync(messageDto.ConversationId, userId);
                foreach (var participant in conversation.Participants)
                {
                    if (participant.Id != userId)
                    {
                        // Kullanıcının kişisel grubuna bildirim gönder
                        await Clients.Group(participant.Id)
                            .SendAsync("NewMessageNotification", new
                            {
                                ConversationId = messageDto.ConversationId,
                                SenderId = userId,
                                SenderName = message.Sender.Username,
                                MessagePreview = GetMessagePreview(message.Content, message.MessageType),
                                Timestamp = DateTime.UtcNow
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                throw;
            }
        }

        public async Task MarkAsRead(string messageId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Mesajı okundu olarak işaretle
                await _messageService.MarkAsReadAsync(messageId, userId);

                // Mesajın ait olduğu konuşmayı bul
                var message = await _messageService.GetMessageByIdAsync(messageId, userId);
                if (message != null)
                {
                    await Clients.OthersInGroup($"conversation_{message.ConversationId}")
                        .SendAsync("MessageRead", messageId, userId, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking message {messageId} as read");
                throw;
            }
        }

        public async Task MarkConversationAsRead(string conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Konuşmadaki tüm mesajları okundu olarak işaretle
                await _messageService.MarkConversationAsReadAsync(conversationId, userId);

                // Konuşma grubuna bilgiyi gönder
                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("ConversationRead", conversationId, userId, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking conversation {conversationId} as read");
                throw;
            }
        }

        public async Task StartTyping(string conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Konuşmanın diğer katılımcılarına kullanıcının yazmaya başladığını bildir
                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("UserTyping", userId, conversationId, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in StartTyping for conversation {conversationId}");
                throw;
            }
        }

        public async Task StopTyping(string conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Konuşmanın diğer katılımcılarına kullanıcının yazmayı bıraktığını bildir
                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("UserTyping", userId, conversationId, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in StopTyping for conversation {conversationId}");
                throw;
            }
        }

        public async Task AddReaction(string messageId, string reactionType)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                var reactionDto = new AddReactionDto
                {
                    MessageId = messageId,
                    ReactionType = reactionType
                };

                var reactionService = Context?.GetHttpContext()?.RequestServices.GetService(typeof(IReactionService)) as IReactionService;
                var reaction = reactionService != null
                    ? await reactionService.AddReactionAsync(reactionDto, userId)
                    : null;

                // Mesajın ait olduğu konuşmayı bul
                var message = await _messageService.GetMessageByIdAsync(messageId, userId);
                if (message != null)
                {
                    // Konuşma grubuna bilgiyi gönder
                    await Clients.Group($"conversation_{message.ConversationId}")
                        .SendAsync("MessageReaction", messageId, userId, reactionType, reaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding reaction to message {messageId}");
                throw;
            }
        }

        public async Task RemoveReaction(string messageId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                // Tepkiyi kaldır
                var reactionService = Context?.GetHttpContext()?.RequestServices.GetService(typeof(IReactionService)) as IReactionService;
                if (reactionService != null)
                {
                    await reactionService.RemoveReactionAsync(messageId, userId);
                }

                // Mesajın ait olduğu konuşmayı bul
                var message = await _messageService.GetMessageByIdAsync(messageId, userId);
                if (message != null)
                {
                    // Konuşma grubuna bilgiyi gönder
                    await Clients.Group($"conversation_{message.ConversationId}")
                        .SendAsync("MessageReactionRemoved", messageId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing reaction from message {messageId}");
                throw;
            }
        }
        public async Task<bool> GetUserOnlineStatus(string userId)
        {
            return await _presenceTracker.IsUserOnline(userId);
        }

        public async Task<DateTime?> GetUserLastSeen(string userId)
        {
            return await _presenceTracker.GetLastSeenAt(userId);
        }

        public async Task<List<string>> GetOnlineUsers()
        {
            var onlineUsers = await _presenceTracker.GetOnlineUsers();
            return onlineUsers.ToList();
        }
        #region Helper Methods

        private string GetUserId()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private string GetMessagePreview(string content, string messageType)
        {
            if (string.IsNullOrEmpty(content))
            {
                switch (messageType)
                {
                    case "image":
                        return "📷 Fotoğraf";
                    case "video":
                        return "🎥 Video";
                    case "audio":
                        return "🎵 Ses";
                    case "file":
                        return "📎 Dosya";
                    default:
                        return "Yeni mesaj";
                }
            }

            // Mesaj içeriğini kısalt
            if (content.Length > 50)
            {
                return content.Substring(0, 47) + "...";
            }

            return content;
        }

        #endregion
    }
}
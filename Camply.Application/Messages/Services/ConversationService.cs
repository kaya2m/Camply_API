using Camply.Application.Messages.DTOs;
using Camply.Application.Messages.Interfaces;
using Camply.Application.Messages.Interfaces.Services;
using Camply.Application.Users.Interfaces;
using Camply.Domain.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Camply.Application.Messages.Services
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _conversationRepository;
        private readonly IUserService _userService; 

        public ConversationService(
            IConversationRepository conversationRepository,
            IUserService userService)
        {
            _conversationRepository = conversationRepository;
            _userService = userService;
        }

        public async Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId, int page = 1, int pageSize = 20)
        {
            int skip = (page - 1) * pageSize;
            var conversations = await _conversationRepository.GetUserConversationsAsync(userId, skip, pageSize);

            var conversationDtos = new List<ConversationDto>();

            foreach (var conversation in conversations)
            {
                var participants = await GetParticipantsAsync(conversation.ParticipantIds);
                var lastMessageSender = participants.FirstOrDefault(p => p.Id == conversation.LastMessageSenderId);

                conversationDtos.Add(new ConversationDto
                {
                    Id = conversation.Id,
                    Participants = participants,
                    LastMessagePreview = conversation.LastMessagePreview,
                    LastMessageSender = lastMessageSender,
                    LastActivityDate = conversation.LastActivityDate,
                    Title = GetConversationTitle(conversation, participants, userId),
                    ImageUrl = GetConversationImage(conversation, participants, userId),
                    IsGroup = conversation.IsGroup,
                    IsVanish = conversation.IsVanish,
                    UnreadCount = GetUnreadCount(conversation, userId),
                    IsMuted = IsMuted(conversation, userId),
                    Status = conversation.Status
                });
            }

            return conversationDtos;
        }

        public async Task<ConversationDto> GetConversationByIdAsync(string id, string userId)
        {
            var conversation = await _conversationRepository.GetConversationByIdAsync(id);
            if (conversation == null) return null;

            // Kullanıcının bu konuşmaya erişim yetkisi var mı?
            if (!conversation.ParticipantIds.Contains(userId)) return null;

            var participants = await GetParticipantsAsync(conversation.ParticipantIds);
            var lastMessageSender = participants.FirstOrDefault(p => p.Id == conversation.LastMessageSenderId);

            return new ConversationDto
            {
                Id = conversation.Id,
                Participants = participants,
                LastMessagePreview = conversation.LastMessagePreview,
                LastMessageSender = lastMessageSender,
                LastActivityDate = conversation.LastActivityDate,
                Title = GetConversationTitle(conversation, participants, userId),
                ImageUrl = GetConversationImage(conversation, participants, userId),
                IsGroup = conversation.IsGroup,
                IsVanish = conversation.IsVanish,
                UnreadCount = GetUnreadCount(conversation, userId),
                IsMuted = IsMuted(conversation, userId),
                Status = conversation.Status
            };
        }

        public async Task<ConversationDto> CreateConversationAsync(CreateConversationDto createConversationDto, string creatorId)
        {
            // Katılımcılar arasına oluşturan kişiyi de ekle
            if (!createConversationDto.ParticipantIds.Contains(creatorId))
            {
                createConversationDto.ParticipantIds.Add(creatorId);
            }

            var conversation = new Conversation
            {
                ParticipantIds = createConversationDto.ParticipantIds,
                Title = createConversationDto.Title,
                IsGroup = createConversationDto.IsGroup,
                CreatedAt = DateTime.UtcNow,
                LastActivityDate = DateTime.UtcNow,
                Status = "active"
            };

            var createdConversation = await _conversationRepository.CreateConversationAsync(conversation);

            var participants = await GetParticipantsAsync(createdConversation.ParticipantIds);

            return new ConversationDto
            {
                Id = createdConversation.Id,
                Participants = participants,
                LastMessagePreview = null,
                LastMessageSender = null,
                LastActivityDate = createdConversation.LastActivityDate,
                Title = GetConversationTitle(createdConversation, participants, creatorId),
                ImageUrl = GetConversationImage(createdConversation, participants, creatorId),
                IsGroup = createdConversation.IsGroup,
                IsVanish = createdConversation.IsVanish,
                UnreadCount = 0,
                IsMuted = false,
                Status = createdConversation.Status
            };
        }

        public async Task<ConversationDto> GetOrCreateDirectConversationAsync(string currentUserId, string otherUserId)
        {
            var conversation = await _conversationRepository.GetOrCreateOneToOneConversationAsync(currentUserId, otherUserId);

            var participants = await GetParticipantsAsync(conversation.ParticipantIds);
            var lastMessageSender = participants.FirstOrDefault(p => p.Id == conversation.LastMessageSenderId);

            return new ConversationDto
            {
                Id = conversation.Id,
                Participants = participants,
                LastMessagePreview = conversation.LastMessagePreview,
                LastMessageSender = lastMessageSender,
                LastActivityDate = conversation.LastActivityDate,
                Title = GetConversationTitle(conversation, participants, currentUserId),
                ImageUrl = GetConversationImage(conversation, participants, currentUserId),
                IsGroup = conversation.IsGroup,
                IsVanish = conversation.IsVanish,
                UnreadCount = GetUnreadCount(conversation, currentUserId),
                IsMuted = IsMuted(conversation, currentUserId),
                Status = conversation.Status
            };
        }

        public async Task MuteConversationAsync(string conversationId, string userId, bool mute)
        {
            await _conversationRepository.MuteConversationAsync(conversationId, userId, mute);
        }

        public async Task ArchiveConversationAsync(string conversationId, string userId)
        {
            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null) return;

            // Kullanıcının bu konuşmaya erişim yetkisi var mı?
            if (!conversation.ParticipantIds.Contains(userId)) return;

            await _conversationRepository.ArchiveConversationAsync(conversationId, userId);
        }

        public async Task DeleteConversationAsync(string conversationId, string userId)
        {
            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null) return;

            // Kullanıcının bu konuşmaya erişim yetkisi var mı?
            if (!conversation.ParticipantIds.Contains(userId)) return;

            await _conversationRepository.DeleteConversationAsync(conversationId, userId);
        }

        #region Helper Methods

        private async Task<List<UserMinimalDto>> GetParticipantsAsync(List<string> participantIds)
        {
            var participants = new List<UserMinimalDto>();

            foreach (var id in participantIds)
            {
                var user = await _userService.GetUserMinimalAsync(id);
                if (user != null)
                {
                    participants.Add(user);
                }
            }

            return participants;
        }

        private string GetConversationTitle(Conversation conversation, List<UserMinimalDto> participants, string currentUserId)
        {
            if (conversation.IsGroup)
            {
                // Grup konuşması ise başlığı kullan
                return !string.IsNullOrEmpty(conversation.Title)
                    ? conversation.Title
                    : "Grup Konuşması";
            }
            else
            {
                // Birebir konuşma ise diğer kullanıcının adını kullan
                var otherUser = participants.FirstOrDefault(p => p.Id != currentUserId);
                return otherUser?.Username ?? "Kullanıcı";
            }
        }

        private string GetConversationImage(Conversation conversation, List<UserMinimalDto> participants, string currentUserId)
        {
            if (conversation.IsGroup)
            {
                // Grup konuşması ise konuşma resmini kullan
                return !string.IsNullOrEmpty(conversation.ImageUrl)
                    ? conversation.ImageUrl
                    : "/images/default-group.png"; // Varsayılan grup resmi
            }
            else
            {
                // Birebir konuşma ise diğer kullanıcının profil resmini kullan
                var otherUser = participants.FirstOrDefault(p => p.Id != currentUserId);
                return otherUser?.ProfilePictureUrl ?? "/images/default-profile.png"; // Varsayılan profil resmi
            }
        }

        private int GetUnreadCount(Conversation conversation, string userId)
        {
            if (conversation.UnreadCount != null && conversation.UnreadCount.ContainsKey(userId))
            {
                return conversation.UnreadCount[userId];
            }

            return 0;
        }

        private bool IsMuted(Conversation conversation, string userId)
        {
            if (conversation.MutedBy != null && conversation.MutedBy.ContainsKey(userId))
            {
                return conversation.MutedBy[userId];
            }

            return false;
        }

        #endregion
    }
}
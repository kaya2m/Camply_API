namespace Camply.Application.Messages.DTOs
{
    public class MessageDto
    {
        public string Id { get; set; }
        public string ConversationId { get; set; }
        public UserMinimalDto Sender { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; }
        public MessageReplyDto ReplyTo { get; set; }
        public List<MediaAttachmentDto> Media { get; set; }
        public bool IsRead { get; set; }
        public List<UserReadDto> ReadBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsSaved { get; set; }
        public List<ReactionDto> Reactions { get; set; }
    }
}

namespace Camply.Application.Messages.DTOs
{
    public class SendMessageDto
    {
        public string ConversationId { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; } = "text";
        public string ReplyToMessageId { get; set; }
        public List<MediaAttachmentDto> Media { get; set; }
    }
}

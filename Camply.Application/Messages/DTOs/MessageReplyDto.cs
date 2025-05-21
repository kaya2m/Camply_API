namespace Camply.Application.Messages.DTOs
{
    public class MessageReplyDto
    {
        public string MessageId { get; set; }
        public string Content { get; set; }
        public UserMinimalDto Sender { get; set; }
    }
}

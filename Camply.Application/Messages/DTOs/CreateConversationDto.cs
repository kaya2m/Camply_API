namespace Camply.Application.Messages.DTOs
{
    public class CreateConversationDto
    {
        public List<string> ParticipantIds { get; set; }
        public string Title { get; set; }
        public bool IsGroup { get; set; }
    }
}

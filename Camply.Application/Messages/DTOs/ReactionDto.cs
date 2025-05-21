using Camply.Application.Messages.DTOs;

public class ReactionDto
{
    public string Id { get; set; }
    public string MessageId { get; set; }
    public UserMinimalDto User { get; set; }
    public string ReactionType { get; set; }
    public DateTime CreatedAt { get; set; }
}

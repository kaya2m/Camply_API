namespace Camply.Application.Locations.DTOs
{
    public class ReviewHelpfulUser
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public bool IsHelpful { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

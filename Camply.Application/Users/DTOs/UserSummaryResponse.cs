namespace Camply.Application.Users.DTOs
{
    public class UserSummaryResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string ProfileImageUrl { get; set; }
        public bool IsFollowedByCurrentUser { get; set; }
    }
}

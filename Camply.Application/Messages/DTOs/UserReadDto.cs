namespace Camply.Application.Messages.DTOs
{
    public class UserReadDto
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string ProfilePictureUrl { get; set; }
        public DateTime ReadAt { get; set; }
    }
}

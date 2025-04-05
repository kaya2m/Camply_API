namespace Camply.Application.Auth.DTOs.Response
{
    public class UserInfo
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string ProfileImageUrl { get; set; }
        public List<string> Roles { get; set; }
    }
}

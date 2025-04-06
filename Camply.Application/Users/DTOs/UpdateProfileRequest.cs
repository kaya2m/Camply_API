using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Users.DTOs
{
    public class UpdateProfileRequest
    {
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        [StringLength(500)]
        public string Bio { get; set; }

        public string ProfileImageUrl { get; set; }
    }
}

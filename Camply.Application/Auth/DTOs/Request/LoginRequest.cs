using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Auth.DTOs.Request
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
}

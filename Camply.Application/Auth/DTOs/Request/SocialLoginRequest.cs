using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Auth.DTOs.Request
{
    public class SocialLoginRequest
    {
        [Required]
        public string Provider { get; set; } // "Google", "Facebook", "Twitter"

        [Required]
        public string AccessToken { get; set; }

        public string IdToken { get; set; } // For Google
    }
}

using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Auth.DTOs.Request
{
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; }

        public string AccessToken { get; set; } // Opsiyonel
    }
}

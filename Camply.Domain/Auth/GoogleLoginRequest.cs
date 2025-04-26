using System.ComponentModel.DataAnnotations;

namespace Camply.Domain.Auth
{
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; }

        public string AccessToken { get; set; } // Opsiyonel
    }
}

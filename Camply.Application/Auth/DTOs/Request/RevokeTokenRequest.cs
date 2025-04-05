using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Auth.DTOs.Request
{
    public class RevokeTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }
}

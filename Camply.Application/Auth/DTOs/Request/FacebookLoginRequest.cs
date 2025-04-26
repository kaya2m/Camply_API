using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Auth.DTOs.Request
{
    public class FacebookLoginRequest
        {
            [Required]
            public string AccessToken { get; set; }
        }
}

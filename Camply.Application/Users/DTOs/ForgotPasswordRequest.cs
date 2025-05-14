using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Users.DTOs
{
    /// <summary>
    /// Şifre sıfırlama isteği için DTO
    /// </summary>
    public class ForgotPasswordRequest
    {
        /// <summary>
        /// Kullanıcının email adresi
        /// </summary>
        public string Email { get; set; }
    }

    /// <summary>
    /// Token ile şifre sıfırlama isteği için DTO
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// Şifre sıfırlama token'ı
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Kullanıcının belirlemek istediği yeni şifre
        /// </summary>
        public string NewPassword { get; set; }
    }
}

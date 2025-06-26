using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Auth.DTOs.Request
{
    public class RegisterRequest
    {
        [Required]
        [StringLength(25, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-25 karakter arasında olmalıdır.")]
        [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "Kullanıcı adı sadece küçük İngilizce harfler, sayılar ve alt çizgi içerebilir.")]
        public string Username { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Ad 3-50 karakter arasında olmalıdır.")]
        [RegularExpression("^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$", ErrorMessage = "Ad sadece İngilizce veya Türkçe harfler içerebilir.")]
        public string Name { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Soyad 2-50 karakter arasında olmalıdır.")]
        [RegularExpression("^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$", ErrorMessage = "Ad sadece İngilizce veya Türkçe harfler içerebilir.")]
        public string Surname { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        public string ConfirmPassword { get; set; }
    }
}

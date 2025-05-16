using System.Threading.Tasks;

namespace Camply.Application.Common.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Şifre sıfırlama maili gönderir
        /// </summary>
        /// <param name="email">Alıcı email adresi</param>
        /// <param name="username">Kullanıcı adı</param>
        /// <param name="code">Şifre sıfırlama code</param>
        /// <returns>İşlem başarılı ise true</returns>
        Task<bool> SendPasswordResetEmailAsync(string email, string username, string code);

        /// <summary>
        /// Şifre değişikliği bildirimi maili gönderir
        /// </summary>
        /// <param name="email">Alıcı email adresi</param>
        /// <param name="username">Kullanıcı adı</param>
        /// <returns>İşlem başarılı ise true</returns>
        Task<bool> SendPasswordChangedEmailAsync(string email, string username);

        /// <summary>
        /// Email doğrulama maili gönderir
        /// </summary>
        /// <param name="email">Alıcı email adresi</param>
        /// <param name="username">Kullanıcı adı</param>
        /// <param name="verificationCode">Email doğrulama code</param>
        /// <returns>İşlem başarılı ise true</returns>
        Task<bool> SendEmailVerificationAsync(string email, string username, string verificationCode);

        /// <summary>
        /// Genel amaçlı email gönderir
        /// </summary>
        /// <param name="to">Alıcı email adresi</param>
        /// <param name="subject">Konu</param>
        /// <param name="body">İçerik (HTML)</param>
        /// <returns>İşlem başarılı ise true</returns>
        Task<bool> SendEmailAsync(string to, string subject, string body);
    }
}
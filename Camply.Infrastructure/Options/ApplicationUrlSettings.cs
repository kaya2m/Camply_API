using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class ApplicationUrlSettings
    {
        public string ApiBaseUrl { get; set; }
        public string MobileAppScheme { get; set; }
        public string WebClientBaseUrl { get; set; }
        public string ResetPasswordPath { get; set; }
        public string EmailVerificationPath { get; set; }

        // Mobil URL'ler için deep link formatı
        public string GetMobileDeepLink(string path, Dictionary<string, string> parameters = null)
        {
            var baseUrl = $"{MobileAppScheme}{path}";

            if (parameters == null || parameters.Count == 0)
                return baseUrl;

            var queryParams = string.Join("&", parameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            return $"{baseUrl}?{queryParams}";
        }

        // Şifre sıfırlama için mobil deep link
        public string GetMobileResetPasswordUrl(string token)
        {
            return GetMobileDeepLink(ResetPasswordPath, new Dictionary<string, string> { { "token", token } });
        }

        // Email doğrulama için mobil deep link
        public string GetMobileEmailVerificationUrl(string token)
        {
            return GetMobileDeepLink(EmailVerificationPath, new Dictionary<string, string> { { "token", token } });
        }

        // Web URL'leri (Yedek olarak, mobil uygulaması olmayan kullanıcılar için)
        public string GetWebResetPasswordUrl(string token)
        {
            return $"{WebClientBaseUrl.TrimEnd('/')}/{ResetPasswordPath}?token={token}";
        }
    }
}

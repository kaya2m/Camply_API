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

        public string GetWebResetPasswordUrl(string token)
        {
            return $"{WebClientBaseUrl.TrimEnd('/')}/{ResetPasswordPath}?token={token}";
        }

        public string GetMobileResetPasswordUrl(string token)
        {
            return $"{MobileAppScheme.TrimEnd('/')}reset-password?token={token}";
        }

        public string GetMobileEmailVerificationUrl(string token)
        {
            return $"{MobileAppScheme.TrimEnd('/')}verify-email?token={token}";
        }

        public string GetMobileDeepLink(string path, Dictionary<string, string> parameters = null)
        {
            StringBuilder urlBuilder = new StringBuilder($"{MobileAppScheme.TrimEnd('/')}/{path.TrimStart('/')}");

            if (parameters != null && parameters.Count > 0)
            {
                urlBuilder.Append("?");
                bool isFirst = true;

                foreach (var param in parameters)
                {
                    if (!isFirst)
                        urlBuilder.Append("&");

                    urlBuilder.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}");
                    isFirst = false;
                }
            }

            return urlBuilder.ToString();
        }
    }
}

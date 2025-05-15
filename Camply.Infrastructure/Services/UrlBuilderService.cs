using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class UrlBuilderService : IUrlBuilderService
    {
        private readonly ApplicationUrlSettings _urlSettings;

        public UrlBuilderService(IOptions<ApplicationUrlSettings> urlSettings)
        {
            _urlSettings = urlSettings.Value;
        }

        public string GetPasswordResetUrl(string token, ClientType clientType = ClientType.Mobile)
        {
            string url;

            if (clientType == ClientType.Mobile)
            {
                url = $"{_urlSettings.MobileAppScheme}{_urlSettings.ResetPasswordPath}?token={token}";
            }
            else
            {
                url = $"{_urlSettings.WebClientBaseUrl.TrimEnd('/')}/{_urlSettings.ResetPasswordPath}?token={token}";
            }

            return url;
        }

        public string GetEmailVerificationUrl(string token, ClientType clientType = ClientType.Mobile)
        {
            if (clientType == ClientType.Mobile)
            {
                return $"{_urlSettings.MobileAppScheme}{_urlSettings.EmailVerificationPath}?token={token}";
            }
            else
            {
                return $"{_urlSettings.WebClientBaseUrl.TrimEnd('/')}/{_urlSettings.EmailVerificationPath}?token={token}";
            }
        }

        public string GetApiUrl(string path = "")
        {
            path = path?.TrimStart('/') ?? "";
            return $"{_urlSettings.ApiBaseUrl.TrimEnd('/')}/{path}";
        }

        public string GetMobileDeepLink(string path, Dictionary<string, string> parameters = null)
        {
            StringBuilder urlBuilder = new StringBuilder($"{_urlSettings.MobileAppScheme}{path.TrimStart('/')}");

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

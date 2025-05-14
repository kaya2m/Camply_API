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
            return clientType == ClientType.Mobile
                ? _urlSettings.GetMobileResetPasswordUrl(token)
                : _urlSettings.GetWebResetPasswordUrl(token);
        }

        public string GetEmailVerificationUrl(string token, ClientType clientType = ClientType.Mobile)
        {
            return clientType == ClientType.Mobile
                ? _urlSettings.GetMobileEmailVerificationUrl(token)
                : $"{_urlSettings.WebClientBaseUrl.TrimEnd('/')}/{_urlSettings.EmailVerificationPath}?token={token}";
        }

        public string GetApiUrl(string path = "")
        {
            path = path?.TrimStart('/') ?? "";
            return $"{_urlSettings.ApiBaseUrl.TrimEnd('/')}/{path}";
        }

        public string GetMobileDeepLink(string path, Dictionary<string, string> parameters = null)
        {
            return _urlSettings.GetMobileDeepLink(path, parameters);
        }
    }
}

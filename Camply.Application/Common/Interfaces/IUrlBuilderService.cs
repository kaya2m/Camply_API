using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Common.Interfaces
{
    public interface IUrlBuilderService
    {
        string GetPasswordResetUrl(string token, ClientType clientType = ClientType.Mobile);
        string GetEmailVerificationUrl(string token, ClientType clientType = ClientType.Mobile);
        string GetApiUrl(string path = "");
        string GetMobileDeepLink(string path, Dictionary<string, string> parameters = null);
    }

    public enum ClientType
    {
        Mobile,
        Web
    }
}

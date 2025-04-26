using Camply.Application.Auth.Models;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.ExternalServices
{
    public class GoogleAuthService
    {
        private readonly SocialLoginSettings _settings;

        public GoogleAuthService(IOptions<SocialLoginSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _settings.Google.ClientId }
                });

                return new GoogleUserInfo
                {
                    Email = payload.Email,
                    Name = payload.Name,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    ProfilePictureUrl = payload.Picture,
                    ProviderKey = payload.Subject
                };
            }
            catch (InvalidJwtException)
            {
                throw new UnauthorizedAccessException("Invalid Google token");
            }
        }
    }
    public class GoogleUserInfo
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string ProviderKey { get; set; }
    }
}

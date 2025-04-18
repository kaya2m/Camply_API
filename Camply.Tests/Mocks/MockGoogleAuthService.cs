using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Tests.Mocks
{
    public interface IGoogleAuthService
    {
        Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken);
    }

    public class MockGoogleAuthService : IGoogleAuthService
    {
        private readonly Dictionary<string, GoogleUserInfo> _validTokens = new Dictionary<string, GoogleUserInfo>();

        public MockGoogleAuthService()
        {
            // Geçerli test token'ları ekleyin
            _validTokens.Add("test-id-token", new GoogleUserInfo
            {
                Email = "test@example.com",
                Name = "Test User",
                ProviderKey = "google-user-id-123"
            });

            _validTokens.Add("test-id-token-existing", new GoogleUserInfo
            {
                Email = "existing@example.com",
                Name = "Existing User",
                ProviderKey = "google-user-id-456"
            });
        }

        public Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken)
        {
            if (_validTokens.TryGetValue(idToken, out var userInfo))
            {
                return Task.FromResult(userInfo);
            }

            throw new UnauthorizedAccessException("Invalid Google token");
        }
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string ProviderKey { get; set; }
    }
}
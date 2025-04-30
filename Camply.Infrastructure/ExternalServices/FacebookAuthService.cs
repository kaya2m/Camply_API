using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Camply.Application.Auth.Models;
using System.Text.Json.Serialization;

namespace Camply.Infrastructure.ExternalServices
{
    public class FacebookAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly SocialLoginSettings _settings;

        public FacebookAuthService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IOptions<SocialLoginSettings> settings)
        {
            _httpClient = httpClientFactory.CreateClient("Facebook");
            _configuration = configuration;
            _settings = settings.Value;
        }

        public async Task<FacebookUserInfo> ValidateAccessTokenAsync(string accessToken)
        {
            try
            {
                string appAccessTokenUrl = $"https://graph.facebook.com/oauth/access_token?client_id={_settings.Facebook.AppId}&client_secret={_settings.Facebook.AppSecret}&grant_type=client_credentials";
                var appTokenResponse = await _httpClient.GetStringAsync(appAccessTokenUrl);
                var appTokenData = JsonSerializer.Deserialize<FacebookAccessTokenResponse>(appTokenResponse);

                if (appTokenData == null || string.IsNullOrEmpty(appTokenData.AccessToken))
                {
                    throw new UnauthorizedAccessException("Failed to obtain Facebook app access token");
                }

                string validateUrl = $"https://graph.facebook.com/debug_token?input_token={accessToken}&access_token={appTokenData.AccessToken}";
                var validationResponse = await _httpClient.GetStringAsync(validateUrl);
                var validation = JsonSerializer.Deserialize<FacebookUserAccessTokenValidation>(validationResponse);

                if (validation == null || !validation.Data.IsValid)
                {
                    throw new UnauthorizedAccessException("Invalid Facebook token");
                }

                string userInfoUrl = $"https://graph.facebook.com/me?fields=email,name&access_token={accessToken}";
                var userInfoResponse = await _httpClient.GetStringAsync(userInfoUrl);
                var userInfo = JsonSerializer.Deserialize<FacebookUserInfoResponse>(userInfoResponse);

                if (userInfo == null)
                {
                    throw new UnauthorizedAccessException("Failed to get user info from Facebook");
                }

                return new FacebookUserInfo
                {
                    Email = userInfo.Email ?? $"{validation.Data.UserId}@facebook.com",
                    Name = userInfo.Name,
                    ProviderKey = validation.Data.UserId
                };
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Error validating Facebook token", ex);
            }
        }
    }

    public class FacebookUserInfo
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string ProviderKey { get; set; }
    }

    public class FacebookAccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }

    public class FacebookUserAccessTokenValidation
    {
        [JsonPropertyName("data")]
        public FacebookTokenData Data { get; set; }
    }
    public class FacebookTokenData
    {
        [JsonPropertyName("app_id")]
        public string AppId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("application")]
        public string Application { get; set; }

        [JsonPropertyName("data_access_expires_at")]
        public long DataAccessExpiresAt { get; set; }

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }

        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [JsonPropertyName("metadata")]
        public FacebookTokenMetadata Metadata { get; set; }

        [JsonPropertyName("scopes")]
        public string[] Scopes { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }

    public class FacebookTokenMetadata
    {
        [JsonPropertyName("auth_type")]
        public string AuthType { get; set; }
    }
    public class FacebookUserInfoResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("email")]
        public string Email { get; set; }
    }
}
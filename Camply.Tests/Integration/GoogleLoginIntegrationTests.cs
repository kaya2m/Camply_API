using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Camply.Application.Auth.DTOs.Request;
using Camply.Application.Auth.DTOs.Response;
using Camply.Domain.Auth;
using Camply.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit;

namespace Camply.Tests.Integration
{
    public class GoogleLoginIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public GoogleLoginIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Gerçek Google servisini mock ile değiştir
                    services.AddScoped<IGoogleAuthService, MockGoogleAuthService>();
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GoogleLogin_ValidToken_ReturnsSuccess()
        {
            // Arrange
            var request = new
            {
                IdToken = "test-id-token"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/social/google", request);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.True(result.Success);
            Assert.NotNull(result.AccessToken);
        }

        [Fact]
        public async Task GoogleLogin_InvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var request = new
            {
                IdToken = "invalid-token"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/social/google", request);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
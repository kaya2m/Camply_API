using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Net;
using Camply.Application.Auth.DTOs.Request;
using Camply.Application.Auth.DTOs.Response;
using Camply.Application.Auth.Models;
using Camply.Application.Auth.Services;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Camply.Infrastructure.ExternalServices;
using Camply.Infrastructure.Services;
using Xunit;

namespace Camply.Tests.Auth
{
    public class GoogleAuthTests
    {
        private readonly Mock<IRepository<User>> _mockUserRepository;
        private readonly Mock<IRepository<Role>> _mockRoleRepository;
        private readonly Mock<IRepository<UserRole>> _mockUserRoleRepository;
        private readonly Mock<IRepository<RefreshToken>> _mockRefreshTokenRepository;
        private readonly Mock<IRepository<SocialLogin>> _mockSocialLoginRepository;
        private readonly Mock<TokenService> _mockTokenService;
        private readonly Mock<ILogger<AuthService>> _mockLogger;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<IOptions<SocialLoginSettings>> _mockSocialLoginSettings;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly AuthService _authService;

        public GoogleAuthTests()
        {
            // Mock oluşturma
            _mockUserRepository = new Mock<IRepository<User>>();
            _mockRoleRepository = new Mock<IRepository<Role>>();
            _mockUserRoleRepository = new Mock<IRepository<UserRole>>();
            _mockRefreshTokenRepository = new Mock<IRepository<RefreshToken>>();
            _mockSocialLoginRepository = new Mock<IRepository<SocialLogin>>();
            _mockTokenService = new Mock<TokenService>(MockBehavior.Loose, new Mock<IOptions<JwtSettings>>().Object);
            _mockLogger = new Mock<ILogger<AuthService>>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            // HttpContext ve IP mocklamak
            var mockHttpContext = new Mock<HttpContext>();
            var mockConnection = new Mock<ConnectionInfo>();
            mockConnection.Setup(c => c.RemoteIpAddress).Returns(IPAddress.Parse("127.0.0.1"));
            mockHttpContext.Setup(c => c.Connection).Returns(mockConnection.Object);
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

            // SocialLoginSettings mocklamak
            var socialLoginSettings = new SocialLoginSettings
            {
                Google = new GoogleSettings
                {
                    ClientId = "test-client-id",
                    ClientSecret = "test-client-secret"
                }
            };
            _mockSocialLoginSettings = new Mock<IOptions<SocialLoginSettings>>();
            _mockSocialLoginSettings.Setup(s => s.Value).Returns(socialLoginSettings);

            // Google Auth Service oluşturma
            _googleAuthService = new MockGoogleAuthService();

            //// Test edilecek servis
            //_authService = new AuthService(
            //    _mockUserRepository.Object,
            //    _mockRoleRepository.Object,
            //    _mockUserRoleRepository.Object,
            //    _mockRefreshTokenRepository.Object,
            //    _mockSocialLoginRepository.Object,
            //    _mockTokenService.Object,
            //    _mockHttpContextAccessor.Object,
            //    _mockLogger.Object,
            //    (GoogleAuthService)_googleAuthService // Burada cast yapabiliriz çünkü kendi mock sınıfımız hem GoogleAuthService hem de IGoogleAuthService
            //);
        }

        [Fact]
        public async Task SocialLoginAsync_ValidGoogleToken_ReturnsSuccessResponse()
        {
            // Arrange
            var fakeEmail = "test@example.com";
            var fakeName = "Test User";
            var fakeProviderKey = "google-user-id-123";

            // 2. User ve role mock'lama
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();

            // Kullanıcının ilk kez giriş yapacağını varsayalım
            _mockSocialLoginRepository.Setup(r => r.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<SocialLogin, bool>>>())).ReturnsAsync((SocialLogin)null);

            _mockUserRepository.Setup(r => r.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync((User)null);

            // Yeni kullanıcı ekleme - User döndüren metot olarak düzeltildi
            var newUser = new User
            {
                Id = userId,
                Email = fakeEmail,
                Username = $"testuser_{DateTime.Now.Ticks}",
                Status = UserStatus.Active,
                IsEmailVerified = true
            };

            _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync(newUser);

            _mockUserRepository.Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1); // SaveChangesAsync genellikle etkilenen satır sayısını döndürür

            // Rol bulma
            var userRole = new Role { Id = roleId, Name = "User" };
            _mockRoleRepository.Setup(r => r.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<Role, bool>>>()))
                .ReturnsAsync(userRole);

            // Kullanıcı rolü ekleme
            var newUserRole = new UserRole { UserId = userId, RoleId = roleId };
            _mockUserRoleRepository.Setup(r => r.AddAsync(It.IsAny<UserRole>()))
                .ReturnsAsync(newUserRole);

            _mockUserRoleRepository.Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1);

            // Sosyal login ekleme
            var newSocialLogin = new SocialLogin
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = "google",
                ProviderKey = fakeProviderKey
            };

            _mockSocialLoginRepository.Setup(r => r.AddAsync(It.IsAny<SocialLogin>()))
                .ReturnsAsync(newSocialLogin);

            _mockSocialLoginRepository.Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1);

            // Kullanıcı rolleri
            _mockUserRoleRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<UserRole, bool>>>()))
                .ReturnsAsync(new List<UserRole> { new UserRole { UserId = userId, RoleId = roleId } });

            // Roller
            _mockRoleRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<Role, bool>>>()))
                .ReturnsAsync(new List<Role> { new Role { Id = roleId, Name = "User" } });

            // Token oluşturma
            _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<List<string>>()))
                .Returns("test-access-token");

            var refreshToken = new RefreshToken
            {
                Token = "test-refresh-token",
                UserId = userId
            };
            _mockTokenService.Setup(t => t.GenerateRefreshToken(It.IsAny<string>()))
                .Returns(refreshToken);

            // Refresh token ekleme
            _mockRefreshTokenRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken>());

            _mockRefreshTokenRepository.Setup(r => r.AddAsync(It.IsAny<RefreshToken>()))
                .ReturnsAsync(refreshToken);

            _mockRefreshTokenRepository.Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1);

            // Update için mocklama
            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));

            // Google login isteği
            var request = new SocialLoginRequest
            {
                Provider = "google",
                IdToken = "test-id-token"
            };

            // Act
            var result = await _authService.SocialLoginAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Social login successful", result.Message);
            Assert.Equal("test-access-token", result.AccessToken);
            Assert.Equal("test-refresh-token", result.RefreshToken);
            Assert.NotNull(result.User);
        }

        [Fact]
        public async Task SocialLoginAsync_InvalidGoogleToken_ReturnsFailureResponse()
        {
            // Arrange
            // Google login isteği
            var request = new SocialLoginRequest
            {
                Provider = "google",
                IdToken = "invalid-id-token"
            };

            // Act
            var result = await _authService.SocialLoginAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An error occurred during social login", result.Message);
        }

        [Fact]
        public async Task SocialLoginAsync_ExistingUser_ReturnsSuccessResponse()
        {
            // Arrange
            var fakeEmail = "existing@example.com";
            var fakeName = "Existing User";
            var fakeProviderKey = "google-user-id-456";

            // 2. Mevcut kullanıcı mocklaması
            var userId = Guid.NewGuid();
            var existingUser = new User
            {
                Id = userId,
                Email = fakeEmail,
                Username = "existinguser",
                Status = UserStatus.Active
            };

            // Mevcut sosyal login kaydı
            var existingSocialLogin = new SocialLogin
            {
                UserId = userId,
                Provider = "google",
                ProviderKey = fakeProviderKey
            };

            _mockSocialLoginRepository.Setup(r => r.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<SocialLogin, bool>>>())).ReturnsAsync(existingSocialLogin);

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            // Kullanıcı rolleri
            _mockUserRoleRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<UserRole, bool>>>()))
                .ReturnsAsync(new List<UserRole> { new UserRole { UserId = userId, RoleId = Guid.NewGuid() } });

            // Roller
            _mockRoleRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<Role, bool>>>()))
                .ReturnsAsync(new List<Role> { new Role { Id = Guid.NewGuid(), Name = "User" } });

            // Token oluşturma
            _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<List<string>>()))
                .Returns("test-access-token");

            var refreshToken = new RefreshToken
            {
                Token = "test-refresh-token",
                UserId = userId
            };
            _mockTokenService.Setup(t => t.GenerateRefreshToken(It.IsAny<string>()))
                .Returns(refreshToken);

            // Refresh token işlemleri
            _mockRefreshTokenRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken>());

            _mockRefreshTokenRepository.Setup(r => r.AddAsync(It.IsAny<RefreshToken>()))
                .ReturnsAsync(refreshToken);

            _mockRefreshTokenRepository.Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1);

            // Update için mocklama
            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));

            // Google login isteği
            var request = new SocialLoginRequest
            {
                Provider = "google",
                IdToken = "test-id-token-existing"
            };

            // Act
            var result = await _authService.SocialLoginAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Social login successful", result.Message);
            Assert.Equal(existingUser.Email, result.User.Email);
        }
    }

    // Interface oluşturma
    public interface IGoogleAuthService
    {
        Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken);
    }

    // Mock GoogleAuthService - hem interface'i hem de base class'ı implemente ediyor
    public class MockGoogleAuthService : GoogleAuthService, IGoogleAuthService
    {
        private readonly Dictionary<string, (string email, string name, string providerId)> _validTokens =
            new Dictionary<string, (string email, string name, string providerId)>();

        public MockGoogleAuthService() : base(null)  // Base sınıfı için boş constructor kullanıyoruz, çünkü metotlarını override edeceğiz
        {
            // Geçerli test token'ları ekleyin
            _validTokens.Add("test-id-token", ("test@example.com", "Test User", "google-user-id-123"));
            _validTokens.Add("test-id-token-existing", ("existing@example.com", "Existing User", "google-user-id-456"));
        }

        // ValidateIdTokenAsync metodunu yeniden tanımlıyoruz
        public new Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken)
        {
            if (_validTokens.TryGetValue(idToken, out var userInfo))
            {
                // Test token için kullanıcı bilgilerini döndür
                return Task.FromResult(new GoogleUserInfo
                {
                    Email = userInfo.email,
                    Name = userInfo.name,
                    ProviderKey = userInfo.providerId
                });
            }

            throw new UnauthorizedAccessException("Invalid Google token");
        }
    }

    // GoogleUserInfo sınıfı, AuthService'in kullanabileceği formatta
    public class GoogleUserInfo
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string ProviderKey { get; set; }
    }
}
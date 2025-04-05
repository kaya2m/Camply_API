using Camply.Application.Auth.DTOs.Request;
using Camply.Application.Auth.DTOs.Response;
using Camply.Application.Auth.Interfaces;
using Camply.Application.Auth.Services;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Camply.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Role> _roleRepository;
        private readonly IRepository<UserRole> _userRoleRepository;
        private readonly IRepository<RefreshToken> _refreshTokenRepository;
        private readonly IRepository<SocialLogin> _socialLoginRepository;
        private readonly TokenService _tokenService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IRepository<User> userRepository,
            IRepository<Role> roleRepository,
            IRepository<UserRole> userRoleRepository,
            IRepository<RefreshToken> refreshTokenRepository,
            IRepository<SocialLogin> socialLoginRepository,
            TokenService tokenService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _userRoleRepository = userRoleRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _socialLoginRepository = socialLoginRepository;
            _tokenService = tokenService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (request.Password != request.ConfirmPassword)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Passwords do not match"
                    };
                }

                var existingUserByEmail = await _userRepository.SingleOrDefaultAsync(u => u.Email == request.Email);
                if (existingUserByEmail != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Email already in use"
                    };
                }

                var existingUserByUsername = await _userRepository.SingleOrDefaultAsync(u => u.Username == request.Username);
                if (existingUserByUsername != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Username already taken"
                    };
                }

                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Status = UserStatus.Active,
                    IsEmailVerified = false
                };

                await _userRepository.AddAsync(newUser);
                await _userRepository.SaveChangesAsync();

                var userRole = await _roleRepository.SingleOrDefaultAsync(r => r.Name == "User");
                if (userRole != null)
                {
                    await _userRoleRepository.AddAsync(new UserRole
                    {
                        UserId = newUser.Id,
                        RoleId = userRole.Id
                    });
                    await _userRoleRepository.SaveChangesAsync();
                }

                // Generate tokens
                string accessToken = _tokenService.GenerateAccessToken(newUser, new List<string> { "User" });
                string ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();
                var refreshToken = _tokenService.GenerateRefreshToken(ipAddress);
                refreshToken.UserId = newUser.Id;

                await _refreshTokenRepository.AddAsync(refreshToken);
                await _refreshTokenRepository.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    Expiration = DateTime.UtcNow.AddMinutes(30), 
                    User = new UserInfo
                    {
                        Id = newUser.Id,
                        Username = newUser.Username,
                        Email = newUser.Email,
                        ProfileImageUrl = newUser.ProfileImageUrl,
                        Roles = new List<string> { "User" }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during registration"
                };
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _userRepository.SingleOrDefaultAsync(u => u.Email == request.Email);
                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                if (user.Status != UserStatus.Active)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Account is not active"
                    };
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                // Get user roles
                var userRoles = await _userRoleRepository.FindAsync(ur => ur.UserId == user.Id);
                var roleIds = userRoles.Select(ur => ur.RoleId);
                var roles = await _roleRepository.FindAsync(r => roleIds.Contains(r.Id));
                var roleNames = roles.Select(r => r.Name).ToList();

                string accessToken = _tokenService.GenerateAccessToken(user, roleNames);
                string ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

                // Revoke existing refresh tokens
                var activeRefreshTokens = await _refreshTokenRepository.FindAsync(
                    rt => rt.UserId == user.Id && !rt.IsRevoked && DateTime.UtcNow < rt.ExpiryDate);

                foreach (var token in activeRefreshTokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIp = ipAddress;
                    token.ReasonRevoked = "Replaced by new token";
                }

                // Create new refresh token
                var refreshToken = _tokenService.GenerateRefreshToken(ipAddress);
                refreshToken.UserId = user.Id;

                await _refreshTokenRepository.AddAsync(refreshToken);
                await _refreshTokenRepository.SaveChangesAsync();

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    Expiration = DateTime.UtcNow.AddMinutes(30), 
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        ProfileImageUrl = user.ProfileImageUrl,
                        Roles = roleNames
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        public async Task<AuthResponse> SocialLoginAsync(SocialLoginRequest request)
        {
            try
            {
                // Note: In a real implementation, you should validate the token with the provider
                // For simplicity, we're assuming the token is valid and extracting user info

                string email;
                string name;
                string providerKey;

                // Get user info from token (placeholder - implement actual validation)
                switch (request.Provider.ToLower())
                {
                    case "google":
                        (email, name, providerKey) = ExtractGoogleUserInfo(request.AccessToken, request.IdToken);
                        break;
                    case "facebook":
                        (email, name, providerKey) = ExtractFacebookUserInfo(request.AccessToken);
                        break;
                    case "twitter":
                        (email, name, providerKey) = ExtractTwitterUserInfo(request.AccessToken);
                        break;
                    default:
                        return new AuthResponse
                        {
                            Success = false,
                            Message = "Unsupported provider"
                        };
                }

                // Check if user with this social login exists
                var socialLogin = await _socialLoginRepository.SingleOrDefaultAsync(
                    sl => sl.Provider == request.Provider && sl.ProviderKey == providerKey);

                User user;

                if (socialLogin != null)
                {
                    // Get existing user
                    user = await _userRepository.GetByIdAsync(socialLogin.UserId);
                    if (user == null)
                    {
                        return new AuthResponse
                        {
                            Success = false,
                            Message = "User not found"
                        };
                    }
                }
                else
                {
                    // Check if user with this email exists
                    user = await _userRepository.SingleOrDefaultAsync(u => u.Email == email);

                    if (user == null)
                    {
                        // Create new user
                        user = new User
                        {
                            Id = Guid.NewGuid(),
                            Username = GenerateUniqueUsername(name).Result,
                            Email = email,
                            Status = UserStatus.Active,
                            IsEmailVerified = true
                        };

                        await _userRepository.AddAsync(user);
                        await _userRepository.SaveChangesAsync();

                        // Assign default role (User)
                        var userRole = await _roleRepository.SingleOrDefaultAsync(r => r.Name == "User");
                        if (userRole != null)
                        {
                            await _userRoleRepository.AddAsync(new UserRole
                            {
                                UserId = user.Id,
                                RoleId = userRole.Id
                            });
                            await _userRoleRepository.SaveChangesAsync();
                        }
                    }

                    // Create social login record
                    await _socialLoginRepository.AddAsync(new SocialLogin
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Provider = request.Provider,
                        ProviderKey = providerKey,
                        ProviderDisplayName = name,
                        AccessToken = request.AccessToken
                    });
                    await _socialLoginRepository.SaveChangesAsync();
                }

                // Get user roles
                var userRoles = await _userRoleRepository.FindAsync(ur => ur.UserId == user.Id);
                var roleIds = userRoles.Select(ur => ur.RoleId);
                var roles = await _roleRepository.FindAsync(r => roleIds.Contains(r.Id));
                var roleNames = roles.Select(r => r.Name).ToList();

                // Generate tokens
                string accessToken = _tokenService.GenerateAccessToken(user, roleNames);
                string ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

                // Revoke existing refresh tokens
                var activeRefreshTokens = await _refreshTokenRepository.FindAsync(
                    rt => rt.UserId == user.Id && !rt.IsRevoked && DateTime.UtcNow < rt.ExpiryDate);

                foreach (var token in activeRefreshTokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIp = ipAddress;
                    token.ReasonRevoked = "Replaced by new token";
                }

                // Create new refresh token
                var refreshToken = _tokenService.GenerateRefreshToken(ipAddress);
                refreshToken.UserId = user.Id;

                await _refreshTokenRepository.AddAsync(refreshToken);
                await _refreshTokenRepository.SaveChangesAsync();

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Social login successful",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    Expiration = DateTime.UtcNow.AddMinutes(30), // Match with JWT settings
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        ProfileImageUrl = user.ProfileImageUrl,
                        Roles = roleNames
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during social login");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during social login"
                };
            }
        }

        public async Task<AuthResponse> RefreshTokenAsync(string token)
        {
            try
            {
                // Find refresh token
                var refreshToken = await _refreshTokenRepository.SingleOrDefaultAsync(
                    rt => rt.Token == token && !rt.IsRevoked && DateTime.UtcNow < rt.ExpiryDate);

                if (refreshToken == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid or expired refresh token"
                    };
                }

                // Get user
                var user = await _userRepository.GetByIdAsync(refreshToken.UserId);
                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Check if user is active
                if (user.Status != UserStatus.Active)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Account is not active"
                    };
                }

                // Get user roles
                var userRoles = await _userRoleRepository.FindAsync(ur => ur.UserId == user.Id);
                var roleIds = userRoles.Select(ur => ur.RoleId);
                var roles = await _roleRepository.FindAsync(r => roleIds.Contains(r.Id));
                var roleNames = roles.Select(r => r.Name).ToList();

                // Generate new tokens
                string accessToken = _tokenService.GenerateAccessToken(user, roleNames);
                string ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

                // Revoke old refresh token
                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                refreshToken.RevokedByIp = ipAddress;
                refreshToken.ReasonRevoked = "Replaced by new token";

                // Create new refresh token
                var newRefreshToken = _tokenService.GenerateRefreshToken(ipAddress);
                newRefreshToken.UserId = user.Id;
                refreshToken.ReplacedByToken = newRefreshToken.Token;

                await _refreshTokenRepository.AddAsync(newRefreshToken);
                await _refreshTokenRepository.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Token refreshed",
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken.Token,
                    Expiration = DateTime.UtcNow.AddMinutes(30), // Match with JWT settings
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        ProfileImageUrl = user.ProfileImageUrl,
                        Roles = roleNames
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred while refreshing token"
                };
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                // Find refresh token
                var refreshToken = await _refreshTokenRepository.SingleOrDefaultAsync(rt => rt.Token == token);
                if (refreshToken == null)
                {
                    return false;
                }

                string ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

                // Revoke token
                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                refreshToken.RevokedByIp = ipAddress;
                refreshToken.ReasonRevoked = "Revoked without replacement";

                await _refreshTokenRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return false;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                _tokenService.GetPrincipalFromExpiredToken(token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region Helper Methods

        private (string email, string name, string providerKey) ExtractGoogleUserInfo(string accessToken, string idToken)
        {
            // In a real implementation, you would validate the token with Google API
            // For this example, we'll just return dummy values

            // Simulated data - replace with actual implementation
            return ($"user_{Guid.NewGuid()}@gmail.com", $"Google User {DateTime.Now.Ticks}", Guid.NewGuid().ToString());
        }

        private (string email, string name, string providerKey) ExtractFacebookUserInfo(string accessToken)
        {
            // In a real implementation, you would validate the token with Facebook Graph API
            // For this example, we'll just return dummy values

            // Simulated data - replace with actual implementation
            return ($"user_{Guid.NewGuid()}@facebook.com", $"Facebook User {DateTime.Now.Ticks}", Guid.NewGuid().ToString());
        }

        private (string email, string name, string providerKey) ExtractTwitterUserInfo(string accessToken)
        {
            // In a real implementation, you would validate the token with Twitter API
            // For this example, we'll just return dummy values

            // Simulated data - replace with actual implementation
            return ($"user_{Guid.NewGuid()}@twitter.com", $"Twitter User {DateTime.Now.Ticks}", Guid.NewGuid().ToString());
        }

        private async Task<string> GenerateUniqueUsername(string baseName)
        {
            // Remove special characters and spaces
            string username = baseName.Replace(" ", "").Replace(".", "").Replace("-", "");

            // Check if username is already taken
            if (!await _userRepository.ExistsAsync(u => u.Username == username))
            {
                return username;
            }

            // Add random suffix
            return $"{username}_{DateTime.Now.Ticks.ToString().Substring(9)}";
        }

        #endregion
    }
}

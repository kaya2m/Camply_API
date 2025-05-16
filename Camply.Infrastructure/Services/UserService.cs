using Camply.Application.Common.Interfaces;
using Camply.Application.Users.DTOs;
using Camply.Application.Users.Interfaces;
using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Post> _postRepository;
        private readonly IRepository<Blog> _blogRepository;
        private readonly IRepository<Follow> _followRepository;
        private readonly IRepository<Role> _roleRepository;
        private readonly IRepository<UserRole> _userRoleRepository;
        private readonly ILogger<UserService> _logger;
        private readonly IEmailService _emailService;
        private readonly CodeSettings _codeSettings;
        private readonly ICodeBuilderService _codeBuilder;
        public UserService(
            IRepository<User> userRepository,
            IRepository<Post> postRepository,
            IRepository<Blog> blogRepository,
            IRepository<Follow> followRepository,
            IRepository<Role> roleRepository,
            IRepository<UserRole> userRoleRepository,
            ILogger<UserService> logger,
            IEmailService emailService,
            ICodeBuilderService codeBuilder,
           IOptions<CodeSettings> codeSettings)
        {
            _userRepository = userRepository;
            _postRepository = postRepository;
            _blogRepository = blogRepository;
            _followRepository = followRepository;
            _roleRepository = roleRepository;
            _userRoleRepository = userRoleRepository;
            _logger = logger;
            _emailService = emailService;
            _codeBuilder = codeBuilder;
            _codeSettings = codeSettings.Value;
        }

        public async Task<UserProfileResponse> GetUserProfileAsync(Guid userId, Guid? currentUserId = null)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                return await BuildUserProfileResponseAsync(user, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user profile for ID {userId}");
                throw;
            }
        }

        public async Task<UserProfileResponse> GetUserProfileByUsernameAsync(string username, Guid? currentUserId = null)
        {
            try
            {
                var user = await _userRepository.SingleOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with username '{username}' not found");
                }

                return await BuildUserProfileResponseAsync(user, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user profile for username '{username}'");
                throw;
            }
        }

        public async Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                if (!string.IsNullOrEmpty(request.Username) && user.Username != request.Username)
                {
                    var existingUser = await _userRepository.SingleOrDefaultAsync(u => u.Username == request.Username);
                    if (existingUser != null)
                    {
                        throw new InvalidOperationException("Username is already taken");
                    }

                    user.Username = request.Username;
                }

                if (request.Bio != null)
                {
                    user.Bio = request.Bio;
                }

                if (request.ProfileImageUrl != null)
                {
                    user.ProfileImageUrl = request.ProfileImageUrl;
                }

                user.LastModifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return await BuildUserProfileResponseAsync(user, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating profile for user ID {userId}");
                throw;
            }
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    throw new UnauthorizedAccessException("Current password is incorrect");
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.LastModifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user ID {userId}");
                throw;
            }
        }

        public async Task<bool> FollowUserAsync(Guid currentUserId, Guid userToFollowId)
        {
            try
            {
                var currentUser = await _userRepository.GetByIdAsync(currentUserId);
                if (currentUser == null)
                {
                    throw new KeyNotFoundException($"Current user with ID {currentUserId} not found");
                }

                var userToFollow = await _userRepository.GetByIdAsync(userToFollowId);
                if (userToFollow == null)
                {
                    throw new KeyNotFoundException($"User to follow with ID {userToFollowId} not found");
                }

                var existingFollow = await _followRepository.SingleOrDefaultAsync(
                    f => f.FollowerId == currentUserId && f.FollowedId == userToFollowId);

                if (existingFollow != null)
                {
                    return true;
                }

                var follow = new Follow
                {
                    Id = Guid.NewGuid(),
                    FollowerId = currentUserId,
                    FollowedId = userToFollowId,
                    CreatedAt = DateTime.UtcNow
                };

                await _followRepository.AddAsync(follow);
                await _followRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error following user. CurrentUserId: {currentUserId}, UserToFollowId: {userToFollowId}");
                throw;
            }
        }

        public async Task<bool> UnfollowUserAsync(Guid currentUserId, Guid userToUnfollowId)
        {
            try
            {
                var follow = await _followRepository.SingleOrDefaultAsync(
                    f => f.FollowerId == currentUserId && f.FollowedId == userToUnfollowId);

                if (follow == null)
                {
                    return true;
                }

                _followRepository.Remove(follow);
                await _followRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unfollowing user. CurrentUserId: {currentUserId}, UserToUnfollowId: {userToUnfollowId}");
                throw;
            }
        }

        public async Task<PagedList<UserSummaryResponse>> GetFollowersAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var followersQuery = await _followRepository.FindAsync(f => f.FollowedId == userId);
                var followers = followersQuery.ToList();

                var totalCount = followers.Count;

                var paginatedFollowers = followers
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var followerIds = paginatedFollowers.Select(f => f.FollowerId).ToList();

                var followerUsers = new List<User>();
                foreach (var id in followerIds)
                {
                    var user = await _userRepository.GetByIdAsync(id);
                    if (user != null)
                    {
                        followerUsers.Add(user);
                    }
                }

                var currentUserFollowing = currentUserId.HasValue
                    ? (await _followRepository.FindAsync(f => f.FollowerId == currentUserId.Value)).ToList()
                    : new List<Follow>();

                // Map to response model
                var followerResponses = followerUsers.Select(u => new UserSummaryResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    ProfileImageUrl = u.ProfileImageUrl,
                    IsFollowedByCurrentUser = currentUserId.HasValue &&
                        currentUserFollowing.Any(f => f.FollowedId == u.Id)
                }).ToList();

                // Create paged list
                var pagedList = new PagedList<UserSummaryResponse>
                {
                    Items = followerResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return pagedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting followers for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedList<UserSummaryResponse>> GetFollowingAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var followingQuery = await _followRepository.FindAsync(f => f.FollowerId == userId);
                var following = followingQuery.ToList();

                var totalCount = following.Count;

                var paginatedFollowing = following
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var followingIds = paginatedFollowing.Select(f => f.FollowedId).ToList();

                var followingUsers = new List<User>();
                foreach (var id in followingIds)
                {
                    var user = await _userRepository.GetByIdAsync(id);
                    if (user != null)
                    {
                        followingUsers.Add(user);
                    }
                }

                var currentUserFollowing = currentUserId.HasValue
                    ? (await _followRepository.FindAsync(f => f.FollowerId == currentUserId.Value)).ToList()
                    : new List<Follow>();

                var followingResponses = followingUsers.Select(u => new UserSummaryResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    ProfileImageUrl = u.ProfileImageUrl,
                    IsFollowedByCurrentUser = currentUserId.HasValue &&
                        currentUserFollowing.Any(f => f.FollowedId == u.Id)
                }).ToList();

                var pagedList = new PagedList<UserSummaryResponse>
                {
                    Items = followingResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return pagedList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting following for user ID {userId}");
                throw;
            }
        }
        public async Task<bool> ForgotPassword(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    throw new ArgumentException("Email cannot be empty");
                }

                User user = await _userRepository.SingleOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.LogInformation($"Password reset requested for non-existent email: {email}");
                    return true; 
                }

                string resetCode = _codeBuilder.GenerateSixDigitCode();

                user.PasswordResetCode = resetCode;
                user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(_codeSettings.CodeExpirationMinutes);
                user.LastModifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                var emailResult = await _emailService.SendPasswordResetEmailAsync(
                    email,
                    user.Username,
                    resetCode
                    ); 

                _logger.LogInformation($"Password reset code generated for user: {user.Id}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during password reset request for email: {email}");
                throw;
            }
        }
        public async Task<bool> VerifyResetCode(string email, string code)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
                {
                    return false;
                }

                var user = await _userRepository.SingleOrDefaultAsync(u =>
                    u.Email == email &&
                    u.PasswordResetCode == code &&
                    u.PasswordResetCodeExpiry > DateTime.UtcNow &&
                    u.Status == UserStatus.Active &&
                    !u.IsDeleted);

                if (user == null)
                {
                    return false;
                }

                user.IsPasswordResetCodeVerified = true;
                user.CodeVerifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying reset code: Email={email}");
                return false;
            }
        }

        public async Task<bool> ResetPassword(string email, string newPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    throw new ArgumentException("Email cannot be empty");
                }


                if (string.IsNullOrEmpty(newPassword))
                {
                    throw new ArgumentException("New password cannot be empty");
                }

                var user = await _userRepository.SingleOrDefaultAsync(u =>
                    u.Email == email &&
                    u.PasswordResetCodeExpiry > DateTime.UtcNow &&
                    u.IsPasswordResetCodeVerified == true &&
                    u.CodeVerifiedAt > DateTime.UtcNow.AddMinutes(-15) && 
                    u.Status == UserStatus.Active &&
                    !u.IsDeleted);

                if (user == null)
                {
                    throw new InvalidOperationException("Invalid or expired password reset code");
                }

                if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
                {
                    throw new ArgumentException("New password cannot be the same as the old password");
                }

                string passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

                user.PasswordHash = passwordHash;
                user.PasswordResetCode = null;
                user.PasswordResetCodeExpiry = null;
                user.IsPasswordResetCodeVerified = false;
                user.CodeVerifiedAt = null;
                user.LastModifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                try
                {
                    await _emailService.SendPasswordChangedEmailAsync(user.Email, user.Username);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, $"Failed to send password change notification email. User ID: {user.Id}");
                }

                _logger.LogInformation($"Password successfully reset. User ID: {user.Id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                throw;
            }
        }
        #region Helper Methods

        private async Task<UserProfileResponse> BuildUserProfileResponseAsync(User user, Guid? currentUserId)
        {
            // Get counts
            var followersCount = (await _followRepository.FindAsync(f => f.FollowedId == user.Id)).Count();
            var followingCount = (await _followRepository.FindAsync(f => f.FollowerId == user.Id)).Count();
            var postsCount = (await _postRepository.FindAsync(p => p.UserId == user.Id)).Count();
            var blogsCount = (await _blogRepository.FindAsync(b => b.UserId == user.Id)).Count();

            // Check if current user is following this user
            var isFollowedByCurrentUser = false;
            if (currentUserId.HasValue && currentUserId.Value != user.Id)
            {
                var follow = await _followRepository.SingleOrDefaultAsync(
                    f => f.FollowerId == currentUserId.Value && f.FollowedId == user.Id);
                isFollowedByCurrentUser = follow != null;
            }

            // Get user roles
            var userRoles = await _userRoleRepository.FindAsync(ur => ur.UserId == user.Id);
            var roleIds = userRoles.Select(ur => ur.RoleId);
            var roles = await _roleRepository.FindAsync(r => roleIds.Contains(r.Id));
            var roleNames = roles.Select(r => r.Name).ToList();

            return new UserProfileResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                ProfileImageUrl = user.ProfileImageUrl,
                Bio = user.Bio,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                PostsCount = postsCount,
                BlogsCount = blogsCount,
                IsCurrentUser = currentUserId.HasValue && currentUserId.Value == user.Id,
                IsFollowedByCurrentUser = isFollowedByCurrentUser,
                Roles = roleNames.ToList()
            };
        }

        public async Task<bool> SendEmailVerificationCodeAsync(Guid userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                if (user.IsEmailVerified)
                {
                    return true;
                }

                string verificationCode = _codeBuilder.GenerateSixDigitCode();

                user.EmailVerificationCode = verificationCode;
                user.EmailVerificationExpiry = DateTime.UtcNow.AddMinutes(_codeSettings.CodeExpirationMinutes);
                user.LastModifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                var emailResult = await _emailService.SendEmailVerificationAsync(
                    user.Email,
                    user.Username,
                    verificationCode);

                _logger.LogInformation($"Email verification code sent for user: {user.Id}");
                return emailResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email verification code for user ID: {userId}");
                throw;
            }
        }

        public async Task<bool> VerifyEmailAsync(string email, string code)
        {
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
                {
                    return false;
                }

                var user = await _userRepository.SingleOrDefaultAsync(u =>
                    u.Email == email &&
                    u.EmailVerificationCode == code &&
                    u.EmailVerificationExpiry > DateTime.UtcNow &&
                    u.Status == UserStatus.Active &&
                    !u.IsDeleted);

                if (user == null)
                {
                    return false;
                }

                user.IsEmailVerified = true;
                user.EmailVerificationCode = null;
                user.EmailVerificationExpiry = null;
                user.LastModifiedAt = DateTime.UtcNow;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation($"Email verified for user: {user.Id}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying email: Email={email}");
                return false;
            }
        }

        public async Task<bool> ResendEmailVerificationCodeAsync(string email)
        {
            try
            {
                var user = await _userRepository.SingleOrDefaultAsync(u =>
                    u.Email == email &&
                    !u.IsEmailVerified &&
                    u.Status == UserStatus.Active &&
                    !u.IsDeleted);

                if (user == null)
                {
                    _logger.LogInformation($"Email verification code resend requested for non-existent or already verified email: {email}");
                    return true;
                }

                return await SendEmailVerificationCodeAsync(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resending email verification code for email: {email}");
                throw;
            }
        }


        #endregion
    }
}

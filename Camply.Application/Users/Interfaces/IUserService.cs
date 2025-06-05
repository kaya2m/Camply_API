using Camply.Application.Common.Interfaces;
using Camply.Application.Messages.DTOs;
using Camply.Application.Users.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Users.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileResponse> GetUserProfileAsync(Guid userId, Guid? currentUserId = null);
        Task<UserProfileResponse> GetUserProfileByUsernameAsync(string username, Guid? currentUserId = null);
        Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<List<UserSearchResponse>> SearchUsersByUsernameAsync(string searchTerm, int limit = 10);
        Task<bool> UpdateProfilePicture(Guid userId, string Url);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
        Task<bool> FollowUserAsync(Guid currentUserId, Guid userToFollowId);
        Task<bool> UnfollowUserAsync(Guid currentUserId, Guid userToUnfollowId);
        Task<PagedList<UserSummaryResponse>> GetFollowersAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedList<UserSummaryResponse>> GetFollowingAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<bool> ForgotPassword(string email);
        Task<bool> VerifyResetCode(string email, string code);
        Task<bool>ResetPassword(string email, string newPassword);

        Task<bool> SendEmailVerificationCodeAsync(Guid userId);
        Task<bool> VerifyEmailAsync(string email, string code);
        Task<bool> ResendEmailVerificationCodeAsync(string email);

        Task<UserMinimalDto> GetUserMinimalAsync(string userId);
        Task<List<UserMinimalDto>> GetUsersMinimalByIdsAsync(List<string> userIds);
    }
}

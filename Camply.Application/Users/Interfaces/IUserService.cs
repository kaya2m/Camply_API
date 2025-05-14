using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Application.Common.Interfaces;
using Camply.Application.Users.DTOs;

namespace Camply.Application.Users.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileResponse> GetUserProfileAsync(Guid userId, Guid? currentUserId = null);
        Task<UserProfileResponse> GetUserProfileByUsernameAsync(string username, Guid? currentUserId = null);
        Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
        Task<bool> FollowUserAsync(Guid currentUserId, Guid userToFollowId);
        Task<bool> UnfollowUserAsync(Guid currentUserId, Guid userToUnfollowId);
        Task<PagedList<UserSummaryResponse>> GetFollowersAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedList<UserSummaryResponse>> GetFollowingAsync(Guid userId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<bool>ResetPassword(string token, string newPassword);
        Task<bool> ForgotPassword(string email, ClientType clientType);
    }
}

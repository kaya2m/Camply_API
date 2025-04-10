﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Application.Users.DTOs;
using Camply.Application.Users.Interfaces;
using Camply.Domain.Auth;
using Camply.Domain.Repositories;
using Camply.Domain;
using Microsoft.Extensions.Logging;

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

        public UserService(
            IRepository<User> userRepository,
            IRepository<Post> postRepository,
            IRepository<Blog> blogRepository,
            IRepository<Follow> followRepository,
            IRepository<Role> roleRepository,
            IRepository<UserRole> userRoleRepository,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _postRepository = postRepository;
            _blogRepository = blogRepository;
            _followRepository = followRepository;
            _roleRepository = roleRepository;
            _userRoleRepository = userRoleRepository;
            _logger = logger;
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

                // Check if username is changed and not already taken
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

                // Verify current password
                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    throw new UnauthorizedAccessException("Current password is incorrect");
                }

                // Update password
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

                // Check if already following
                var existingFollow = await _followRepository.SingleOrDefaultAsync(
                    f => f.FollowerId == currentUserId && f.FollowedId == userToFollowId);

                if (existingFollow != null)
                {
                    return true;
                }

                // Create new follow
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
                    // Not following
                    return true;
                }

                // Remove follow
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
                // Get all followers for this user
                var followersQuery = await _followRepository.FindAsync(f => f.FollowedId == userId);
                var followers = followersQuery.ToList();

                // Get total count
                var totalCount = followers.Count;

                // Apply pagination
                var paginatedFollowers = followers
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Get follower user IDs
                var followerIds = paginatedFollowers.Select(f => f.FollowerId).ToList();

                // Get follower users
                var followerUsers = new List<User>();
                foreach (var id in followerIds)
                {
                    var user = await _userRepository.GetByIdAsync(id);
                    if (user != null)
                    {
                        followerUsers.Add(user);
                    }
                }

                // Check if current user follows these users
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
                // Get all users this user follows
                var followingQuery = await _followRepository.FindAsync(f => f.FollowerId == userId);
                var following = followingQuery.ToList();

                // Get total count
                var totalCount = following.Count;

                // Apply pagination
                var paginatedFollowing = following
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Get following user IDs
                var followingIds = paginatedFollowing.Select(f => f.FollowedId).ToList();

                // Get following users
                var followingUsers = new List<User>();
                foreach (var id in followingIds)
                {
                    var user = await _userRepository.GetByIdAsync(id);
                    if (user != null)
                    {
                        followingUsers.Add(user);
                    }
                }

                // Check if current user follows these users
                var currentUserFollowing = currentUserId.HasValue
                    ? (await _followRepository.FindAsync(f => f.FollowerId == currentUserId.Value)).ToList()
                    : new List<Follow>();

                // Map to response model
                var followingResponses = followingUsers.Select(u => new UserSummaryResponse
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

        #endregion
    }
}

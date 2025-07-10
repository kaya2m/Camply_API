using Camply.Application.Common.Models;
using Camply.Application.Users.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Application.Users.Interfaces
{
    public interface IUserRecommendationService
    {
        Task<PagedResponse<UserRecommendationResponse>> GetUserRecommendationsAsync(UserRecommendationRequest request);
        Task<List<UserRecommendationResponse>> GetPopularUsersAsync(Guid currentUserId, int count = 10);
        Task<List<UserRecommendationResponse>> GetMutualFollowersRecommendationsAsync(Guid currentUserId, int count = 10);
        Task<List<UserRecommendationResponse>> GetRecentActiveUsersAsync(Guid currentUserId, int count = 10);
        Task<List<UserRecommendationResponse>> GetSimilarUsersAsync(Guid currentUserId, int count = 10);
        Task RefreshUserRecommendationsAsync(Guid userId);
        Task<List<string>> GetRecommendationReasonsAsync(Guid currentUserId, Guid recommendedUserId);
    }
}
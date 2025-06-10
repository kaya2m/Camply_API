using Camply.Application.Common.Models;
using Camply.Application.Posts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLFeedAlgorithmService
    {
        Task<PagedResponse<PostSummaryResponseML>> GeneratePersonalizedFeedAsync(Guid userId, int page, int pageSize);
        Task<List<PostSummaryResponseML>> GetTrendingPostsAsync(int count = 20);
        Task<List<PostSummaryResponseML>> GetSimilarPostsAsync(Guid postId, int count = 10);
        Task TrackUserInteractionAsync(Guid userId, Guid postId, string interactionType, double duration = 0);
        Task RefreshUserFeedCacheAsync(Guid userId);
        Task<double> PredictEngagementScoreAsync(Guid userId, Guid postId);
    }
}

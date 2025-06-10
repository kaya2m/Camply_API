using Camply.Application.Common.Models;
using Camply.Application.Posts.DTOs;
using Camply.Domain.Analytics;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IContextAwareFeedService
    {
        Task<UserContext> BuildUserContextAsync(Guid userId, HttpContext httpContext);
        Task<List<PostSummaryResponseML>> GetContextualizedFeedAsync(Guid userId, UserContext context, int count);
        Task<UserContext> EnrichContextWithLocationDataAsync(UserContext context);
        Task<double> CalculateContextualBoostAsync(PostSummaryResponseML post, UserContext context);
    }
}

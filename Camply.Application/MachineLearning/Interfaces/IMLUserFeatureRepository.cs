using Camply.Domain.MachineLearning;
using Camply.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLUserFeatureRepository : IRepository<MLUserFeature>
    {
        Task<MLUserFeature> GetLatestUserFeatureAsync(Guid userId, string featureType);
        Task<List<MLUserFeature>> GetUserFeaturesAsync(Guid userId, string version = null);
        Task<bool> UpdateUserFeatureAsync(Guid userId, string featureType, string featureVector, float qualityScore);
        Task<List<MLUserFeature>> GetUsersNeedingFeatureUpdateAsync(string featureType, TimeSpan maxAge, int limit = 100);
    }
}

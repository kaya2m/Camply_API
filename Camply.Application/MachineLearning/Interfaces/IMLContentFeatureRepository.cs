using Camply.Domain.MachineLearning;
using Camply.Domain.Repositories;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLContentFeatureRepository : IRepository<MLContentFeature>
    {
        Task<MLContentFeature> GetLatestContentFeatureAsync(Guid contentId, string contentType, string featureCategory);
        Task<List<MLContentFeature>> GetContentFeaturesAsync(Guid contentId, string contentType);
        Task<bool> UpdateContentFeatureAsync(Guid contentId, string contentType, string featureCategory, string featureVector, float qualityScore);
        Task<List<MLContentFeature>> GetHighQualityContentAsync(string contentType, float minQualityScore, int limit = 100);
        Task<List<MLContentFeature>> GetViralContentAsync(float minViralScore, int limit = 50);
    }
}

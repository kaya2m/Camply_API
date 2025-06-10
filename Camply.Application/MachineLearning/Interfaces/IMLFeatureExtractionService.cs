namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLFeatureExtractionService
    {
        Task<string> ExtractUserFeaturesAsync(Guid userId);
        Task<string> ExtractContentFeaturesAsync(Guid contentId, string contentType);
        Task<bool> UpdateUserInterestProfileAsync(Guid userId);
        Task<Dictionary<string, double>> CalculateContentSimilarityAsync(Guid contentId1, Guid contentId2);
    }
}

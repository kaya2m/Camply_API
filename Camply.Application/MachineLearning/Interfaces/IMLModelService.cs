namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLModelService
    {
        Task<double> PredictEngagementAsync(string userFeatures, string contentFeatures);
        Task<List<Guid>> RecommendContentAsync(Guid userId, int count);
        Task<bool> UpdateModelAsync(string modelType, string modelPath);
        Task<Dictionary<string, double>> GetModelMetricsAsync(string modelType);
    }
}

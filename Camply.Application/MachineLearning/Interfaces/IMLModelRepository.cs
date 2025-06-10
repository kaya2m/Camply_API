using Camply.Domain.MachineLearning;
using Camply.Domain.Repositories;

namespace Camply.Application.MachineLearning.Interfaces
{
    public interface IMLModelRepository : IRepository<MLModel>
    {
        Task<MLModel> GetActiveModelAsync(string modelType);
        Task<List<MLModel>> GetModelVersionsAsync(string modelName);
        Task<bool> SetActiveModelAsync(Guid modelId);
        Task<bool> DeactivateModelAsync(Guid modelId);
        Task<List<MLModel>> GetModelsByTypeAsync(string modelType);
    }
}

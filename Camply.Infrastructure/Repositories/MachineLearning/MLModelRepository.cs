using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain.MachineLearning;
using Camply.Infrastructure.Data;
using Camply.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Repositories.MachineLearning
{
    public class MLModelRepository : Repository<MLModel>, IMLModelRepository
    {
        public MLModelRepository(CamplyDbContext context) : base(context) { }

        public async Task<MLModel> GetActiveModelAsync(string modelType)
        {
            return await _dbSet
                .Where(m => m.ModelType == modelType && m.IsActive)
                .OrderByDescending(m => m.TrainedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<MLModel>> GetModelVersionsAsync(string modelName)
        {
            return await _dbSet
                .Where(m => m.Name == modelName)
                .OrderByDescending(m => m.TrainedAt)
                .ToListAsync();
        }

        public async Task<bool> SetActiveModelAsync(Guid modelId)
        {
            var model = await GetByIdAsync(modelId);
            if (model == null) return false;

            // Aynı türdeki diğer modelleri deaktif et
            var otherModels = await _dbSet
                .Where(m => m.ModelType == model.ModelType && m.Id != modelId && m.IsActive)
                .ToListAsync();

            foreach (var otherModel in otherModels)
            {
                otherModel.IsActive = false;
                Update(otherModel);
            }

            // Bu modeli aktif et
            model.IsActive = true;
            Update(model);

            return await SaveChangesAsync() > 0;
        }

        public async Task<bool> DeactivateModelAsync(Guid modelId)
        {
            var model = await GetByIdAsync(modelId);
            if (model == null) return false;

            model.IsActive = false;
            Update(model);

            return await SaveChangesAsync() > 0;
        }

        public async Task<List<MLModel>> GetModelsByTypeAsync(string modelType)
        {
            return await _dbSet
                .Where(m => m.ModelType == modelType)
                .OrderByDescending(m => m.TrainedAt)
                .ToListAsync();
        }
    }
}

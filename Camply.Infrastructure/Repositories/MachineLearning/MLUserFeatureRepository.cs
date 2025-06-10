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
    public class MLUserFeatureRepository : Repository<MLUserFeature>, IMLUserFeatureRepository
    {
        public MLUserFeatureRepository(CamplyDbContext context) : base(context) { }

        public async Task<MLUserFeature> GetLatestUserFeatureAsync(Guid userId, string featureType)
        {
            return await _dbSet
                .Where(uf => uf.UserId == userId && uf.FeatureType == featureType)
                .OrderByDescending(uf => uf.LastCalculated)
                .FirstOrDefaultAsync();
        }

        public async Task<List<MLUserFeature>> GetUserFeaturesAsync(Guid userId, string version = null)
        {
            var query = _dbSet.Where(uf => uf.UserId == userId);

            if (!string.IsNullOrEmpty(version))
                query = query.Where(uf => uf.Version == version);

            return await query.OrderByDescending(uf => uf.LastCalculated).ToListAsync();
        }

        public async Task<bool> UpdateUserFeatureAsync(Guid userId, string featureType, string featureVector, float qualityScore)
        {
            var existingFeature = await GetLatestUserFeatureAsync(userId, featureType);

            if (existingFeature != null)
            {
                existingFeature.FeatureVector = featureVector;
                existingFeature.QualityScore = qualityScore;
                existingFeature.LastCalculated = DateTime.UtcNow;
                existingFeature.LastModifiedAt = DateTime.UtcNow;

                Update(existingFeature);
            }
            else
            {
                await AddAsync(new MLUserFeature
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FeatureType = featureType,
                    FeatureVector = featureVector,
                    QualityScore = qualityScore,
                    Version = "v1.0",
                    LastCalculated = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }

            return await SaveChangesAsync() > 0;
        }

        public async Task<List<MLUserFeature>> GetUsersNeedingFeatureUpdateAsync(string featureType, TimeSpan maxAge, int limit = 100)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;

            return await _dbSet
                .Where(uf => uf.FeatureType == featureType && uf.LastCalculated < cutoffTime)
                .OrderBy(uf => uf.LastCalculated)
                .Take(limit)
                .ToListAsync();
        }
    }
}

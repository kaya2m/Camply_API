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
    public class MLContentFeatureRepository : Repository<MLContentFeature>, IMLContentFeatureRepository
    {
        public MLContentFeatureRepository(CamplyDbContext context) : base(context) { }

        public async Task<MLContentFeature> GetLatestContentFeatureAsync(Guid contentId, string contentType, string featureCategory)
        {
            return  await _dbSet
                .Where(cf => cf.ContentId == contentId && cf.ContentType == contentType && cf.FeatureCategory == featureCategory)
                .OrderByDescending(cf => cf.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<MLContentFeature>> GetContentFeaturesAsync(Guid contentId, string contentType)
        {
            return await _dbSet
                .Where(cf => cf.ContentId == contentId && cf.ContentType == contentType)
                .OrderByDescending(cf => cf.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateContentFeatureAsync(Guid contentId, string contentType, string featureCategory, string featureVector, double qualityScore)
        {
            var existingFeature = await GetLatestContentFeatureAsync(contentId, contentType, featureCategory);

            if (existingFeature != null)
            {
                existingFeature.FeatureVector = featureVector;
                existingFeature.QualityScore = ((float)qualityScore);
                existingFeature.LastModifiedAt = DateTime.UtcNow;

                Update(existingFeature);
            }
            else
            {
                await AddAsync(new MLContentFeature
                {
                    Id = Guid.NewGuid(),
                    ContentId = contentId,
                    ContentType = contentType,
                    FeatureCategory = featureCategory,
                    FeatureVector = featureVector,
                    QualityScore = ((float)qualityScore),
                    Version = "v1.0",
                    CreatedAt = DateTime.UtcNow
                });
            }

            return await SaveChangesAsync() > 0;
        }

        public async Task<List<MLContentFeature>> GetHighQualityContentAsync(string contentType, float minQualityScore, int limit = 100)
        {
            return await _dbSet
                .Where(cf => cf.ContentType == contentType && cf.QualityScore >= minQualityScore)
                .OrderByDescending(cf => cf.QualityScore)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<MLContentFeature>> GetViralContentAsync(float minViralScore, int limit = 50)
        {
            return await _dbSet
                .Where(cf => cf.ViralPotential >= minViralScore)
                .OrderByDescending(cf => cf.ViralPotential)
                .Take(limit)
                .ToListAsync();
        }
    }
}

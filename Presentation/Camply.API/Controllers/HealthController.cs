using Camply.Domain.Analytics;
using Camply.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace Camply.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly CamplyDbContext _dbContext;
        private readonly MongoDbContext _mongoContext;
        private readonly ILogger<HealthController> _logger;

        public HealthController(CamplyDbContext dbContext, MongoDbContext mongoContext, ILogger<HealthController> logger)
        {
            _dbContext = dbContext;
            _mongoContext = mongoContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var health = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Checks = new
                {
                    PostgreSQL = await CheckPostgreSQL(),
                    MongoDB = await CheckMongoDB(),
                    MLFeatures = await CheckMLFeatures()
                }
            };

            return Ok(health);
        }

        [HttpGet("ml")]
        public async Task<IActionResult> GetMLHealth()
        {
            var mlHealth = new
            {
                Status = "Healthy",
                Features = new
                {
                    UserFeatures = await _dbContext.MLUserFeatures.CountAsync(),
                    ContentFeatures = await _dbContext.MLContentFeatures.CountAsync(),
                    ActiveModels = await _dbContext.MLModels.CountAsync(m => m.IsActive),
                    RunningExperiments = await _dbContext.MLExperiments.CountAsync(e => e.Status == "running")
                },
                MongoDB = new
                {
                    UserInteractions = await _mongoContext.UserInteractions.CountDocumentsAsync(FilterDefinition<UserInteractionDocument>.Empty),
                    UserInterests = await _mongoContext.UserInterests.CountDocumentsAsync(FilterDefinition<UserInterestDocument>.Empty),
                    CachedFeeds = await _mongoContext.FeedCache.CountDocumentsAsync(FilterDefinition<FeedCacheDocument>.Empty)
                }
            };

            return Ok(mlHealth);
        }

        private async Task<object> CheckPostgreSQL()
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                return new { Status = "Healthy", ResponseTime = "< 100ms" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostgreSQL health check failed");
                return new { Status = "Unhealthy", Error = ex.Message };
            }
        }

        private async Task<object> CheckMongoDB()
        {
            try
            {
                var isConnected = await _mongoContext.CheckConnectionAsync();
                return new { Status = isConnected ? "Healthy" : "Unhealthy" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB health check failed");
                return new { Status = "Unhealthy", Error = ex.Message };
            }
        }

        private async Task<object> CheckMLFeatures()
        {
            try
            {
                var activeModels = await _dbContext.MLModels.CountAsync(m => m.IsActive);
                var recentFeatures = await _dbContext.MLUserFeatures
                    .CountAsync(f => f.LastCalculated > DateTime.UtcNow.AddDays(-1));

                return new
                {
                    Status = activeModels > 0 ? "Healthy" : "Warning",
                    ActiveModels = activeModels,
                    RecentFeatures = recentFeatures
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ML features health check failed");
                return new { Status = "Unhealthy", Error = ex.Message };
            }
        }
    }
}

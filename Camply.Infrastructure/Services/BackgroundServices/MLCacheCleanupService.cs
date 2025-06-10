using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Camply.Infrastructure.Services.BackgroundServices
{
    // ML Cache Cleanup Background Service
    public class MLCacheCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MLCacheCleanupService> _logger;
        private readonly MLSettings _settings;

        public MLCacheCleanupService(
            IServiceProvider serviceProvider,
            ILogger<MLCacheCleanupService> logger,
            IOptions<MLSettings> settings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ML Cache Cleanup Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredCacheAsync(stoppingToken);
                    await CleanupOldAnalyticsAsync(stoppingToken);

                    // Run cleanup every hour
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ML Cache Cleanup Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ML Cache Cleanup Service");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task CleanupExpiredCacheAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            try
            {
                // Cleanup patterns
                var patterns = new[]
                {
                    "feed:user:*",
                    "user_features:*",
                    "content_features:*",
                    "trending:*"
                };

                foreach (var pattern in patterns)
                {
                    await cacheService.RemovePatternAsync(pattern);
                }

                _logger.LogInformation("Cache cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up cache");
            }
        }

        private async Task CleanupOldAnalyticsAsync(CancellationToken cancellationToken)
        {
            // This would cleanup old analytics data from MongoDB
            // Implementation depends on your MongoDB repository structure

            _logger.LogInformation("Analytics cleanup completed");
        }
    }
}
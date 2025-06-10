using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Camply.Infrastructure.Services.BackgroundServices
{
    public class MLFeatureCalculationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MLFeatureCalculationService> _logger;
        private readonly MLSettings _settings;

        public MLFeatureCalculationService(
            IServiceProvider serviceProvider,
            ILogger<MLFeatureCalculationService> logger,
            IOptions<MLSettings> settings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ML Feature Calculation Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CalculateUserFeaturesAsync(stoppingToken);
                    await CalculateContentFeaturesAsync(stoppingToken);

                    // Wait for the configured interval
                    var delay = TimeSpan.FromHours(_settings.FeatureCalculation.UserFeatureUpdateIntervalHours);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ML Feature Calculation Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ML Feature Calculation Service");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retrying
                }
            }
        }

        private async Task CalculateUserFeaturesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var featureService = scope.ServiceProvider.GetRequiredService<IMLFeatureExtractionService>();
            var userFeatureRepository = scope.ServiceProvider.GetRequiredService<IMLUserFeatureRepository>();

            try
            {
                // Get users who need feature updates
                var maxAge = TimeSpan.FromHours(_settings.FeatureCalculation.UserFeatureUpdateIntervalHours);
                var usersNeedingUpdate = await userFeatureRepository.GetUsersNeedingFeatureUpdateAsync(
                    "behavioral", maxAge, _settings.FeatureCalculation.BatchSize);

                _logger.LogInformation("Updating features for {Count} users", usersNeedingUpdate.Count);

                var semaphore = new SemaphoreSlim(_settings.FeatureCalculation.MaxConcurrentJobs);
                var tasks = usersNeedingUpdate.Select(async userFeature =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await featureService.ExtractUserFeaturesAsync(userFeature.UserId);
                        await featureService.UpdateUserInterestProfileAsync(userFeature.UserId);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating user features");
            }
        }

        private async Task CalculateContentFeaturesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var featureService = scope.ServiceProvider.GetRequiredService<IMLFeatureExtractionService>();
            var postRepository = scope.ServiceProvider.GetRequiredService<IRepository<Post>>();

            try
            {
                // Get recent posts that need feature extraction
                var cutoffTime = DateTime.UtcNow.AddHours(-_settings.FeatureCalculation.ContentFeatureUpdateIntervalHours);
                var recentPosts = await postRepository.FindAsync(p =>
                    p.CreatedAt >= cutoffTime &&
                    p.Status == PostStatus.Active);

                _logger.LogInformation("Extracting features for {Count} posts", recentPosts.Count());

                var semaphore = new SemaphoreSlim(_settings.FeatureCalculation.MaxConcurrentJobs);
                var tasks = recentPosts.Take(_settings.FeatureCalculation.BatchSize).Select(async post =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await featureService.ExtractContentFeaturesAsync(post.Id, "Post");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating content features");
            }
        }
    }
}
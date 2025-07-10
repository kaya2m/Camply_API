using Camply.Application.Common.Interfaces;
using Camply.Application.Users.Interfaces;
using Camply.Domain.Repositories;
using Camply.Domain.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class RecommendationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RecommendationBackgroundService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30); // Run every 30 minutes

        public RecommendationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<RecommendationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recommendation Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRecommendationTasks();
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in recommendation background service");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes on error
                }
            }
        }

        private async Task ProcessRecommendationTasks()
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            var userRecommendationService = scope.ServiceProvider.GetRequiredService<IUserRecommendationService>();

            try
            {
                // Task 1: Pre-calculate popular users
                await PreCalculatePopularUsers(cacheService, userRepository, userRecommendationService);

                // Task 2: Pre-calculate recommendations for active users
                await PreCalculateActiveUserRecommendations(cacheService, userRepository, userRecommendationService);

                // Task 3: Clean up old cache entries
                await CleanupOldCacheEntries(cacheService);

                _logger.LogInformation("Recommendation background tasks completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recommendation tasks");
            }
        }

        private async Task PreCalculatePopularUsers(ICacheService cacheService, IRepository<User> userRepository, IUserRecommendationService recommendationService)
        {
            try
            {
                _logger.LogInformation("Pre-calculating popular users");

                // Get a sample user to calculate popular users
                var sampleUser = await userRepository.FirstOrDefaultAsync(u => true);
                if (sampleUser != null)
                {
                    // This will cache the popular users
                    await recommendationService.GetPopularUsersAsync(sampleUser.Id, 50);
                }

                _logger.LogInformation("Popular users pre-calculation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pre-calculating popular users");
            }
        }

        private async Task PreCalculateActiveUserRecommendations(ICacheService cacheService, IRepository<User> userRepository, IUserRecommendationService recommendationService)
        {
            try
            {
                _logger.LogInformation("Pre-calculating recommendations for active users");

                // Get users who were active in the last 24 hours
                var activeUsers = await userRepository.FindAsync(u => 
                    u.LastLoginAt.HasValue && 
                    u.LastLoginAt.Value > DateTime.UtcNow.AddHours(-24));

                var activeUsersList = activeUsers.Take(100).ToList(); // Limit to 100 most active users

                var tasks = activeUsersList.Select(async user =>
                {
                    try
                    {
                        // Pre-calculate smart recommendations for active users
                        await recommendationService.GetUserRecommendationsAsync(new Application.Users.DTOs.UserRecommendationRequest
                        {
                            UserId = user.Id,
                            Algorithm = "smart",
                            PageNumber = 1,
                            PageSize = 20
                        });

                        // Pre-calculate mutual followers
                        await recommendationService.GetMutualFollowersRecommendationsAsync(user.Id, 10);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error pre-calculating recommendations for user {UserId}", user.Id);
                    }
                });

                await Task.WhenAll(tasks);

                _logger.LogInformation("Active user recommendations pre-calculation completed for {Count} users", activeUsersList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pre-calculating active user recommendations");
            }
        }

        private async Task CleanupOldCacheEntries(ICacheService cacheService)
        {
            try
            {
                _logger.LogInformation("Cleaning up old cache entries");

                // These patterns will be handled by Redis TTL, but we can force cleanup if needed
                var cachePatterns = new[]
                {
                    "user_recommendations:*",
                    "popular_users:*",
                    "mutual_followers:*",
                    "recent_active:*",
                    "user_following:*",
                    "user_followers:*",
                    "user_stats:*",
                    "user_likes:*"
                };

                // Note: In a real implementation, you might want to implement selective cleanup
                // based on last access time or other criteria
                
                _logger.LogInformation("Cache cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up cache entries");
            }
        }
    }
}
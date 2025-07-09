using Camply.Application.Common.Interfaces;
using Camply.Application.Posts.Interfaces;
using Camply.Domain.Repositories;
using Camply.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Camply.Domain.Auth;

namespace Camply.Infrastructure.Services
{
    public class FeedCacheWarmupService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<FeedCacheWarmupService> _logger;

        public FeedCacheWarmupService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<FeedCacheWarmupService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var services = scope.ServiceProvider;

                    var postService = services.GetRequiredService<IPostService>();
                    var userRepository = services.GetRequiredService<IRepository<User>>();
                    var followRepository = services.GetRequiredService<IRepository<Follow>>();
                    var cacheService = services.GetRequiredService<ICacheService>();

                    await WarmupActiveUserFeeds(postService, userRepository, followRepository, cacheService);
                    await WarmupPopularContent(postService);

                    // Run every 30 minutes
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during feed cache warmup");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task WarmupActiveUserFeeds(
            IPostService postService,
            IRepository<User> userRepository,
            IRepository<Follow> followRepository,
            ICacheService cacheService)
        {
            try
            {
                var activeUsers = await userRepository.FindAsync(u =>
                    u.LastLoginAt.HasValue &&
                    u.LastLoginAt.Value > DateTime.UtcNow.AddHours(-24));

                var activeUsersList = activeUsers.Take(100).ToList(); 

                _logger.LogInformation($"Warming up feeds for {activeUsersList.Count} active users");

                var batches = activeUsersList.Chunk(10);

                foreach (var batch in batches)
                {
                    var tasks = batch.Select(user => WarmupUserFeed(user.Id, postService, followRepository, cacheService));
                    await Task.WhenAll(tasks);
                }

                _logger.LogInformation("Feed warmup completed for active users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming up active user feeds");
            }
        }

        private async Task WarmupUserFeed(
            Guid userId,
            IPostService postService,
            IRepository<Follow> followRepository,
            ICacheService cacheService)
        {
            try
            {
                await postService.GetFeedAsync(userId, 1, 20);
                await postService.GetFeedAsync(userId, 2, 20);

                var followingQuery = await followRepository.FindAsync(f => f.FollowerId == userId);
                var followingIds = followingQuery.Select(f => f.FollowedId).ToList();
                var cacheKey = $"following:{userId}";
                await cacheService.SetAsync(cacheKey, followingIds, TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error warming up feed for user {userId}");
            }
        }

        private async Task WarmupPopularContent(IPostService postService)
        {
            try
            {
                // Pre-warm general posts (popular and recent)
                await postService.GetPostsAsync(1, 20, "popular");
                await postService.GetPostsAsync(1, 20, "recent");
                await postService.GetPostsAsync(2, 20, "popular");
                await postService.GetPostsAsync(2, 20, "recent");

                _logger.LogInformation("Popular content warmup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming up popular content");
            }
        }
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int chunkSize)
        {
            if (chunkSize <= 0)
                throw new ArgumentException("Chunk size must be greater than 0.", nameof(chunkSize));

            var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var chunk = new T[chunkSize];
                chunk[0] = enumerator.Current;
                var chunkIndex = 1;

                while (chunkIndex < chunkSize && enumerator.MoveNext())
                {
                    chunk[chunkIndex] = enumerator.Current;
                    chunkIndex++;
                }

                if (chunkIndex < chunkSize)
                {
                    var smallerChunk = new T[chunkIndex];
                    Array.Copy(chunk, smallerChunk, chunkIndex);
                    yield return smallerChunk;
                }
                else
                {
                    yield return chunk;
                }
            }
        }
    }
}
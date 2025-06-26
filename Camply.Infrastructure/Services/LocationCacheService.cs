using Camply.Application.Common.Interfaces;
using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Camply.Infrastructure.Services
{
    public class LocationCacheService : ILocationCacheService
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<LocationCacheService> _logger;

        private const string LOCATION_KEY_PREFIX = "location";
        private const string SEARCH_KEY_PREFIX = "location_search";
        private const string SPONSORED_KEY = "sponsored_locations";
        private const string NEARBY_KEY_PREFIX = "nearby_locations";
        private const string USER_BOOKMARKS_PREFIX = "user_bookmarks";

        private readonly TimeSpan _defaultLocationExpiry = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _searchResultsExpiry = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _sponsoredExpiry = TimeSpan.FromHours(1);
        private readonly TimeSpan _nearbyExpiry = TimeSpan.FromMinutes(20);
        private readonly TimeSpan _bookmarksExpiry = TimeSpan.FromHours(2);

        public LocationCacheService(
            ICacheService cacheService,
            ILogger<LocationCacheService> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        #region Location Cache Operations

        public async Task<LocationDetailResponse> GetCachedLocationAsync(Guid locationId)
        {
            try
            {
                var key = GenerateLocationCacheKey(locationId);
                var cachedLocation = await _cacheService.GetAsync<LocationDetailResponse>(key);

                if (cachedLocation != null)
                {
                    _logger.LogDebug("Location found in cache: {LocationId}", locationId);
                }

                return cachedLocation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached location {LocationId}", locationId);
                return null;
            }
        }

        public async Task SetCachedLocationAsync(Guid locationId, LocationDetailResponse location, TimeSpan? expiration = null)
        {
            try
            {
                if (location == null)
                {
                    _logger.LogWarning("Attempted to cache null location for ID: {LocationId}", locationId);
                    return;
                }

                var key = GenerateLocationCacheKey(locationId);
                var expiry = expiration ?? _defaultLocationExpiry;

                await _cacheService.SetAsync(key, location, expiry);
                _logger.LogDebug("Location cached successfully: {LocationId}", locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching location {LocationId}", locationId);
            }
        }

        public async Task RemoveCachedLocationAsync(Guid locationId)
        {
            try
            {
                var key = GenerateLocationCacheKey(locationId);
                await _cacheService.RemoveAsync(key);
                _logger.LogDebug("Location removed from cache: {LocationId}", locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached location {LocationId}", locationId);
            }
        }

        #endregion

        #region Search Results Cache

        public async Task<PagedResponse<LocationSummaryResponse>> GetCachedSearchResultsAsync(string searchKey)
        {
            try
            {
                var key = $"{SEARCH_KEY_PREFIX}:{searchKey}";
                var cachedResults = await _cacheService.GetAsync<PagedResponse<LocationSummaryResponse>>(key);

                if (cachedResults != null)
                {
                    _logger.LogDebug("Search results found in cache: {SearchKey}", searchKey);
                }

                return cachedResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached search results for key: {SearchKey}", searchKey);
                return null;
            }
        }

        public async Task SetCachedSearchResultsAsync(string searchKey, PagedResponse<LocationSummaryResponse> results, TimeSpan? expiration = null)
        {
            try
            {
                if (results == null || !results.Items.Any())
                {
                    _logger.LogDebug("Not caching empty search results for key: {SearchKey}", searchKey);
                    return;
                }

                var key = $"{SEARCH_KEY_PREFIX}:{searchKey}";
                var expiry = expiration ?? _searchResultsExpiry;

                await _cacheService.SetAsync(key, results, expiry);
                _logger.LogDebug("Search results cached successfully: {SearchKey}", searchKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching search results for key: {SearchKey}", searchKey);
            }
        }

        public async Task InvalidateSearchCacheAsync()
        {
            try
            {
                var pattern = $"{SEARCH_KEY_PREFIX}:*";
                var deletedCount = await _cacheService.RemovePatternAsync(pattern);
                _logger.LogInformation("Invalidated {Count} search cache entries", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating search cache");
            }
        }

        #endregion

        #region Sponsored Locations Cache

        public async Task<List<LocationSummaryResponse>> GetCachedSponsoredLocationsAsync()
        {
            try
            {
                var cachedSponsored = await _cacheService.GetAsync<List<LocationSummaryResponse>>(SPONSORED_KEY);

                if (cachedSponsored != null)
                {
                    _logger.LogDebug("Sponsored locations found in cache");
                }

                return cachedSponsored;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached sponsored locations");
                return null;
            }
        }

        public async Task SetCachedSponsoredLocationsAsync(List<LocationSummaryResponse> locations, TimeSpan? expiration = null)
        {
            try
            {
                if (locations == null || !locations.Any())
                {
                    _logger.LogDebug("Not caching empty sponsored locations list");
                    return;
                }

                var expiry = expiration ?? _sponsoredExpiry;
                await _cacheService.SetAsync(SPONSORED_KEY, locations, expiry);
                _logger.LogDebug("Sponsored locations cached successfully. Count: {Count}", locations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching sponsored locations");
            }
        }

        public async Task InvalidateSponsoredCacheAsync()
        {
            try
            {
                await _cacheService.RemoveAsync(SPONSORED_KEY);
                _logger.LogDebug("Sponsored locations cache invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating sponsored cache");
            }
        }

        #endregion

        #region Nearby Locations Cache

        public async Task<PagedResponse<LocationSummaryResponse>> GetCachedNearbyLocationsAsync(double latitude, double longitude, double radius)
        {
            try
            {
                var key = GenerateNearbyCacheKey(latitude, longitude, radius);
                var cachedNearby = await _cacheService.GetAsync<PagedResponse<LocationSummaryResponse>>(key);

                if (cachedNearby != null)
                {
                    _logger.LogDebug("Nearby locations found in cache: {Latitude}, {Longitude}, {Radius}km", latitude, longitude, radius);
                }

                return cachedNearby;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached nearby locations");
                return null;
            }
        }

        public async Task SetCachedNearbyLocationsAsync(double latitude, double longitude, double radius, PagedResponse<LocationSummaryResponse> locations, TimeSpan? expiration = null)
        {
            try
            {
                if (locations == null || !locations.Items.Any())
                {
                    _logger.LogDebug("Not caching empty nearby locations");
                    return;
                }

                var key = GenerateNearbyCacheKey(latitude, longitude, radius);
                var expiry = expiration ?? _nearbyExpiry;

                await _cacheService.SetAsync(key, locations, expiry);
                _logger.LogDebug("Nearby locations cached successfully: {Latitude}, {Longitude}, {Radius}km", latitude, longitude, radius);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching nearby locations");
            }
        }

        #endregion

        #region User Bookmarks Cache

        public async Task<List<Guid>> GetCachedUserBookmarksAsync(Guid userId)
        {
            try
            {
                var key = $"{USER_BOOKMARKS_PREFIX}:{userId}";
                var cachedBookmarks = await _cacheService.GetAsync<List<Guid>>(key);

                if (cachedBookmarks != null)
                {
                    _logger.LogDebug("User bookmarks found in cache: {UserId}", userId);
                    return cachedBookmarks;
                }

                return new List<Guid>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached user bookmarks for user {UserId}", userId);
                return new List<Guid>();
            }
        }

        public async Task SetCachedUserBookmarksAsync(Guid userId, List<Guid> bookmarks, TimeSpan? expiration = null)
        {
            try
            {
                var key = $"{USER_BOOKMARKS_PREFIX}:{userId}";
                var expiry = expiration ?? _bookmarksExpiry;

                await _cacheService.SetAsync(key, bookmarks ?? new List<Guid>(), expiry);
                _logger.LogDebug("User bookmarks cached successfully: {UserId}, Count: {Count}", userId, bookmarks?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching user bookmarks for user {UserId}", userId);
            }
        }

        public async Task AddBookmarkToCacheAsync(Guid userId, Guid locationId)
        {
            try
            {
                var key = $"{USER_BOOKMARKS_PREFIX}:{userId}";
                var currentBookmarks = await GetCachedUserBookmarksAsync(userId);

                if (!currentBookmarks.Contains(locationId))
                {
                    currentBookmarks.Add(locationId);
                    await SetCachedUserBookmarksAsync(userId, currentBookmarks);
                }

                _logger.LogDebug("Bookmark added to cache: {UserId} -> {LocationId}", userId, locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding bookmark to cache for user {UserId}, location {LocationId}", userId, locationId);
            }
        }

        public async Task RemoveBookmarkFromCacheAsync(Guid userId, Guid locationId)
        {
            try
            {
                var key = $"{USER_BOOKMARKS_PREFIX}:{userId}";
                var currentBookmarks = await GetCachedUserBookmarksAsync(userId);

                if (currentBookmarks.Remove(locationId))
                {
                    await SetCachedUserBookmarksAsync(userId, currentBookmarks);
                }

                _logger.LogDebug("Bookmark removed from cache: {UserId} -> {LocationId}", userId, locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing bookmark from cache for user {UserId}, location {LocationId}", userId, locationId);
            }
        }

        #endregion

        #region Cache Utility Methods

        public async Task InvalidateLocationCacheAsync(Guid locationId)
        {
            try
            {
                await RemoveCachedLocationAsync(locationId);

                // Tüm search cache'i temizle çünkü bu lokasyon arama sonuçlarında olabilir
                await InvalidateSearchCacheAsync();

                // Nearby cache'i de temizle
                var nearbyPattern = $"{NEARBY_KEY_PREFIX}:*";
                await _cacheService.RemovePatternAsync(nearbyPattern);

                _logger.LogInformation("Location cache invalidated: {LocationId}", locationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating location cache for {LocationId}", locationId);
            }
        }

        public async Task InvalidateUserLocationCacheAsync(Guid userId)
        {
            try
            {
                var key = $"{USER_BOOKMARKS_PREFIX}:{userId}";
                await _cacheService.RemoveAsync(key);
                _logger.LogDebug("User location cache invalidated: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user location cache for {UserId}", userId);
            }
        }

        public string GenerateLocationCacheKey(Guid locationId)
        {
            return $"{LOCATION_KEY_PREFIX}:{locationId}";
        }

        public string GenerateSearchCacheKey(LocationSearchRequest request)
        {
            try
            {
                // Search parametrelerini hash'e dönüştür
                var searchParams = new
                {
                    Query = request.Query?.ToLower().Trim(),
                    Types = request.Types?.OrderBy(t => t).ToList(),
                    Features = request.Features?.OrderBy(f => f).ToList(),
                    MinPrice = request.MinPrice,
                    MaxPrice = request.MaxPrice,
                    MinRating = request.MinRating,
                    IsSponsored = request.IsSponsored,
                    HasEntryFee = request.HasEntryFee,
                    Latitude = request.Latitude?.ToString("F6"), // 6 decimal precision
                    Longitude = request.Longitude?.ToString("F6"),
                    RadiusKm = request.RadiusKm,
                    SortBy = request.SortBy,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                var json = JsonSerializer.Serialize(searchParams);
                var hash = ComputeHash(json);

                return $"{SEARCH_KEY_PREFIX}:{hash}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating search cache key");
                return $"{SEARCH_KEY_PREFIX}:fallback_{DateTime.UtcNow.Ticks}";
            }
        }

        public string GenerateNearbyCacheKey(double latitude, double longitude, double radius)
        {
            // Koordinatları yuvarla (yaklaşık 100m hassasiyet)
            var roundedLat = Math.Round(latitude, 3);
            var roundedLng = Math.Round(longitude, 3);
            var roundedRadius = Math.Round(radius, 1);

            return $"{NEARBY_KEY_PREFIX}:{roundedLat}_{roundedLng}_{roundedRadius}";
        }

        #endregion

        #region Advanced Cache Operations

        /// <summary>
        /// Lokasyon ile ilgili tüm cache'leri temizler
        /// </summary>
        public async Task InvalidateAllLocationRelatedCacheAsync()
        {
            try
            {
                var tasks = new List<Task>
                {
                    InvalidateSearchCacheAsync(),
                    InvalidateSponsoredCacheAsync(),
                    _cacheService.RemovePatternAsync($"{LOCATION_KEY_PREFIX}:*"),
                    _cacheService.RemovePatternAsync($"{NEARBY_KEY_PREFIX}:*")
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("All location-related caches invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all location-related caches");
            }
        }

        /// <summary>
        /// Cache istatistiklerini döndürür
        /// </summary>
        public async Task<LocationCacheStats> GetCacheStatsAsync()
        {
            try
            {
                var stats = new LocationCacheStats();

                // Location cache sayısı
                var locationPattern = $"{LOCATION_KEY_PREFIX}:*";
                stats.LocationCacheCount = await CountKeysAsync(locationPattern);

                // Search cache sayısı
                var searchPattern = $"{SEARCH_KEY_PREFIX}:*";
                stats.SearchCacheCount = await CountKeysAsync(searchPattern);

                // Nearby cache sayısı
                var nearbyPattern = $"{NEARBY_KEY_PREFIX}:*";
                stats.NearbyCacheCount = await CountKeysAsync(nearbyPattern);

                // User bookmarks cache sayısı
                var bookmarksPattern = $"{USER_BOOKMARKS_PREFIX}:*";
                stats.UserBookmarksCacheCount = await CountKeysAsync(bookmarksPattern);

                // Sponsored cache var mı?
                stats.HasSponsoredCache = await _cacheService.ExistsAsync(SPONSORED_KEY);

                stats.LastUpdated = DateTime.UtcNow;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache stats");
                return new LocationCacheStats { LastUpdated = DateTime.UtcNow };
            }
        }

        /// <summary>
        /// Belirli bir kullanıcının tüm location cache'lerini temizler
        /// </summary>
        public async Task InvalidateUserSpecificCacheAsync(Guid userId)
        {
            try
            {
                var tasks = new List<Task>
                {
                    InvalidateUserLocationCacheAsync(userId),
                    // Kullanıcının search cache'lerini de temizleyebiliriz
                    // (Eğer kullanıcı bazında search cache tutuyorsak)
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("User-specific caches invalidated: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user-specific cache for {UserId}", userId);
            }
        }

        /// <summary>
        /// Cache warmup - popüler lokasyonları önceden cache'e yükler
        /// </summary>
        public async Task WarmupCacheAsync(List<LocationDetailResponse> popularLocations)
        {
            try
            {
                var tasks = popularLocations.Select(location =>
                    SetCachedLocationAsync(location.Id, location, TimeSpan.FromHours(2)));

                await Task.WhenAll(tasks);
                _logger.LogInformation("Cache warmed up with {Count} popular locations", popularLocations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming up cache");
            }
        }

        /// <summary>
        /// Coğrafi bölge bazında cache temizleme
        /// </summary>
        public async Task InvalidateRegionalCacheAsync(string country, string city = null)
        {
            try
            {
                // Bu bölgedeki lokasyonları içeren tüm cache'leri temizle
                await InvalidateSearchCacheAsync();

                var nearbyPattern = $"{NEARBY_KEY_PREFIX}:*";
                await _cacheService.RemovePatternAsync(nearbyPattern);

                _logger.LogInformation("Regional cache invalidated for {Country}/{City}", country, city);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating regional cache for {Country}/{City}", country, city);
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<long> CountKeysAsync(string pattern)
        {
            try
            {
                // Redis'te key sayısını almak için basit bir yöntem
                // Gerçek implementasyonda SCAN komutu kullanılabilir
                return 0; // Placeholder - Redis implementation'a göre güncellenebilir
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting keys for pattern: {Pattern}", pattern);
                return 0;
            }
        }

        private string ComputeHash(string input)
        {
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing hash");
                return DateTime.UtcNow.Ticks.ToString();
            }
        }

        #endregion
    }

    #region Supporting Classes

    public class LocationCacheStats
    {
        public long LocationCacheCount { get; set; }
        public long SearchCacheCount { get; set; }
        public long NearbyCacheCount { get; set; }
        public long UserBookmarksCacheCount { get; set; }
        public bool HasSponsoredCache { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CachePerformanceMetrics
    {
        public double LocationCacheHitRate { get; set; }
        public double SearchCacheHitRate { get; set; }
        public double AverageResponseTime { get; set; }
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    #endregion
}
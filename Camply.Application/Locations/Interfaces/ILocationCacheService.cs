using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Locations.Interfaces
{
    public interface ILocationCacheService
    {
        // Lokasyon cache işlemleri
        Task<LocationDetailResponse> GetCachedLocationAsync(Guid locationId);
        Task SetCachedLocationAsync(Guid locationId, LocationDetailResponse location, TimeSpan? expiration = null);
        Task RemoveCachedLocationAsync(Guid locationId);

        // Arama sonuçları cache
        Task<PagedResponse<LocationSummaryResponse>> GetCachedSearchResultsAsync(string searchKey);
        Task SetCachedSearchResultsAsync(string searchKey, PagedResponse<LocationSummaryResponse> results, TimeSpan? expiration = null);
        Task InvalidateSearchCacheAsync();

        // Sponsorlu lokasyonlar cache
        Task<List<LocationSummaryResponse>> GetCachedSponsoredLocationsAsync();
        Task SetCachedSponsoredLocationsAsync(List<LocationSummaryResponse> locations, TimeSpan? expiration = null);
        Task InvalidateSponsoredCacheAsync();

        // Nearby lokasyonlar cache (koordinat bazlı)
        Task<PagedResponse<LocationSummaryResponse>> GetCachedNearbyLocationsAsync(double latitude, double longitude, double radius);
        Task SetCachedNearbyLocationsAsync(double latitude, double longitude, double radius, PagedResponse<LocationSummaryResponse> locations, TimeSpan? expiration = null);

        // Kullanıcı bookmark cache
        Task<List<Guid>> GetCachedUserBookmarksAsync(Guid userId);
        Task SetCachedUserBookmarksAsync(Guid userId, List<Guid> bookmarks, TimeSpan? expiration = null);
        Task AddBookmarkToCacheAsync(Guid userId, Guid locationId);
        Task RemoveBookmarkFromCacheAsync(Guid userId, Guid locationId);

        // Cache utility
        Task InvalidateLocationCacheAsync(Guid locationId);
        Task InvalidateUserLocationCacheAsync(Guid userId);
        string GenerateLocationCacheKey(Guid locationId);
        string GenerateSearchCacheKey(LocationSearchRequest request);
        string GenerateNearbyCacheKey(double latitude, double longitude, double radius);
    }
}

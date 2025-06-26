using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Locations.Interfaces
{
    public interface ILocationAnalyticsService
    {
        // Görüntüleme istatistikleri
        Task RecordLocationViewAsync(Guid locationId, Guid? userId, string ipAddress = null);
        Task<LocationViewStats> GetLocationViewStatsAsync(Guid locationId, DateTime? startDate = null, DateTime? endDate = null);
        Task<Dictionary<string, int>> GetMonthlyViewStatsAsync(Guid locationId, int months = 12);

        // Popülerlik analizi
        Task<PagedResponse<LocationSummaryResponse>> GetTrendingLocationsAsync(int pageNumber, int pageSize, TimeSpan? timeRange = null);
        Task<PagedResponse<LocationSummaryResponse>> GetMostReviewedLocationsAsync(int pageNumber, int pageSize, TimeSpan? timeRange = null);
        Task<PagedResponse<LocationSummaryResponse>> GetHighestRatedLocationsAsync(int pageNumber, int pageSize, double minRating = 4.0);

        // Bölgesel istatistikler
        Task<Dictionary<string, int>> GetLocationStatsByRegionAsync(string country = null, string city = null);
        Task<Dictionary<LocationType, int>> GetLocationStatsByTypeAsync();
        Task<Dictionary<string, int>> GetPopularFeaturesAsync();

        // Kullanıcı davranış analizi
        Task<Dictionary<string, object>> GetUserLocationInteractionStatsAsync(Guid userId);
        Task<PagedResponse<LocationSummaryResponse>> GetRecommendedLocationsAsync(Guid userId, int pageNumber, int pageSize);

        // Admin raporları
        Task<LocationAnalyticsReport> GenerateLocationReportAsync(Guid locationId, DateTime startDate, DateTime endDate);
        Task<GlobalLocationReport> GenerateGlobalLocationReportAsync(DateTime startDate, DateTime endDate);

        Task RecordLocationInteractionAsync(Guid locationId, Guid userId, string interactionType, Dictionary<string, object> metadata = null);
        Task RecordSearchMetricsAsync(string searchTerm, List<string> filters, int resultCount, Guid? userId = null);
    }
}

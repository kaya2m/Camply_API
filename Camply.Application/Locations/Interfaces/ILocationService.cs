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
    public interface ILocationService
    {
        // Temel CRUD işlemleri
        Task<LocationDetailResponse> GetLocationByIdAsync(Guid locationId, Guid? currentUserId = null);
        Task<PagedResponse<LocationSummaryResponse>> GetLocationsAsync(LocationSearchRequest request, Guid? currentUserId = null);
        Task<LocationDetailResponse> CreateLocationAsync(Guid userId, CreateLocationRequest request);
        Task<LocationDetailResponse> UpdateLocationAsync(Guid locationId, Guid userId, UpdateLocationRequest request);
        Task<bool> DeleteLocationAsync(Guid locationId, Guid userId);

        // Arama ve filtreleme
        Task<PagedResponse<LocationSummaryResponse>> SearchLocationsAsync(LocationSearchRequest request, Guid? currentUserId = null);
        Task<LocationFilterOptions> GetFilterOptionsAsync();
        Task<PagedResponse<LocationSummaryResponse>> GetLocationsByTypeAsync(LocationType type, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<LocationSummaryResponse>> GetLocationsByFeatureAsync(string feature, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<PagedResponse<LocationSummaryResponse>> GetNearbyLocationsAsync(double latitude, double longitude, double radiusKm, int pageNumber, int pageSize, Guid? currentUserId = null);

        // Sponsorlu lokasyonlar
        Task<PagedResponse<LocationSummaryResponse>> GetSponsoredLocationsAsync(int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<bool> SetSponsorshipAsync(Guid locationId, SponsorshipRequest request);
        Task<bool> RemoveSponsorshipAsync(Guid locationId);

        // Onay işlemleri (Admin)
        Task<PagedResponse<LocationSummaryResponse>> GetPendingLocationsAsync(int pageNumber, int pageSize);
        Task<bool> ApproveLocationAsync(Guid locationId, Guid adminUserId, AdminLocationApprovalRequest request);
        Task<bool> RejectLocationAsync(Guid locationId, Guid adminUserId, AdminLocationApprovalRequest request);

        // Bookmark işlemleri
        Task<bool> BookmarkLocationAsync(Guid locationId, Guid userId);
        Task<bool> UnbookmarkLocationAsync(Guid locationId, Guid userId);
        Task<PagedResponse<LocationSummaryResponse>> GetBookmarkedLocationsAsync(Guid userId, int pageNumber, int pageSize);

        // İstatistikler
        Task<LocationStatisticsResponse> GetLocationStatisticsAsync(Guid locationId);
        Task IncrementViewCountAsync(Guid locationId, Guid? userId = null);

        // Konum doğrulama
        Task<bool> VerifyLocationCoordinatesAsync(double latitude, double longitude, string address);
    }
}

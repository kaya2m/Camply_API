using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Locations.Interfaces
{
    public interface ILocationReviewService
    {
        // Review CRUD işlemleri
        Task<LocationReviewDetailResponse> GetReviewByIdAsync(Guid reviewId, Guid? currentUserId = null);
        Task<PagedResponse<LocationReviewSummaryResponse>> GetLocationReviewsAsync(Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null);
        Task<LocationReviewDetailResponse> CreateReviewAsync(Guid locationId, Guid userId, CreateLocationReviewRequest request);
        Task<LocationReviewDetailResponse> UpdateReviewAsync(Guid reviewId, Guid userId, UpdateLocationReviewRequest request);
        Task<bool> DeleteReviewAsync(Guid reviewId, Guid userId);

        // Review yardımcı işlemleri
        Task<bool> MarkReviewHelpfulAsync(Guid reviewId, Guid userId, ReviewHelpfulRequest request);
        Task<bool> RemoveReviewHelpfulAsync(Guid reviewId, Guid userId);

        // Sahip yanıtları
        Task<bool> AddOwnerResponseAsync(Guid reviewId, Guid ownerId, OwnerResponseRequest request);
        Task<bool> UpdateOwnerResponseAsync(Guid reviewId, Guid ownerId, OwnerResponseRequest request);
        Task<bool> RemoveOwnerResponseAsync(Guid reviewId, Guid ownerId);

        // İstatistikler
        Task<LocationRatingBreakdown> GetLocationRatingBreakdownAsync(Guid locationId);
        Task<PagedResponse<LocationReviewSummaryResponse>> GetUserReviewsAsync(Guid userId, int pageNumber, int pageSize);

        // Doğrulama
        Task<bool> VerifyReviewAsync(Guid reviewId, Guid adminUserId);
        Task<bool> HasUserVisitedLocationAsync(Guid userId, Guid locationId);
    }
}

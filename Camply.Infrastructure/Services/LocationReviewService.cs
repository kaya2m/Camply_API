using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Camply.Application.Media.Interfaces;
using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class LocationReviewService : ILocationReviewService
    {
        private readonly IRepository<LocationReview> _reviewRepository;
        private readonly IRepository<Location> _locationRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<ReviewHelpful> _helpfulRepository;
        private readonly IRepository<Media> _mediaRepository;
        private readonly ILocationCacheService _cacheService;
        private readonly IMediaService _mediaService;
        private readonly ILogger<LocationReviewService> _logger;

        public LocationReviewService(
            IRepository<LocationReview> reviewRepository,
            IRepository<Location> locationRepository,
            IRepository<User> userRepository,
            IRepository<ReviewHelpful> helpfulRepository,
            IRepository<Media> mediaRepository,
            ILocationCacheService cacheService,
            IMediaService mediaService,
            ILogger<LocationReviewService> logger)
        {
            _reviewRepository = reviewRepository;
            _locationRepository = locationRepository;
            _userRepository = userRepository;
            _helpfulRepository = helpfulRepository;
            _mediaRepository = mediaRepository;
            _cacheService = cacheService;
            _mediaService = mediaService;
            _logger = logger;
        }

        public async Task<LocationReviewDetailResponse> GetReviewByIdAsync(Guid reviewId, Guid? currentUserId = null)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                return await MapToReviewDetailResponse(review, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review: {ReviewId}", reviewId);
                throw;
            }
        }

        public async Task<PagedResponse<LocationReviewSummaryResponse>> GetLocationReviewsAsync(Guid locationId, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var reviews = await _reviewRepository.FindAsync(r =>
                    r.LocationId == locationId && !r.IsDeleted);

                var reviewList = reviews.OrderByDescending(r => r.CreatedAt).ToList();
                var totalCount = reviewList.Count;

                var paginatedReviews = reviewList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var responses = new List<LocationReviewSummaryResponse>();
                foreach (var review in paginatedReviews)
                {
                    responses.Add(await MapToReviewSummaryResponse(review, currentUserId));
                }

                return new PagedResponse<LocationReviewSummaryResponse>
                {
                    Items = responses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reviews for location: {LocationId}", locationId);
                throw;
            }
        }

        public async Task<LocationReviewDetailResponse> CreateReviewAsync(Guid locationId, Guid userId, CreateLocationReviewRequest request)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Kullanıcının bu lokasyon için zaten review'u var mı kontrol et
                var existingReview = await _reviewRepository.SingleOrDefaultAsync(r =>
                    r.LocationId == locationId && r.UserId == userId && !r.IsDeleted);

                if (existingReview != null)
                {
                    throw new InvalidOperationException("You have already reviewed this location");
                }

                var review = new LocationReview
                {
                    Id = Guid.NewGuid(),
                    LocationId = locationId,
                    UserId = userId,
                    Title = request.Title,
                    Content = request.Content,
                    OverallRating = request.OverallRating,
                    CleanlinessRating = request.CleanlinessRating,
                    ServiceRating = request.ServiceRating,
                    LocationRating = request.LocationRating,
                    ValueRating = request.ValueRating,
                    FacilitiesRating = request.FacilitiesRating,
                    IsRecommended = request.IsRecommended,
                    VisitDate = request.VisitDate,
                    StayDuration = request.StayDuration,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                await _reviewRepository.AddAsync(review);
                await _reviewRepository.SaveChangesAsync();

                // Fotoğrafları ekle
                if (request.PhotoIds.Any())
                {
                    await AttachPhotosToReviewAsync(review.Id, request.PhotoIds, userId);
                }

                // Lokasyonun ortalama rating'ini güncelle
                await UpdateLocationAverageRatingAsync(locationId);

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(locationId);

                _logger.LogInformation("Review created successfully: {ReviewId} for location {LocationId} by user {UserId}",
                    review.Id, locationId, userId);

                return await MapToReviewDetailResponse(review, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review for location {LocationId} by user {UserId}", locationId, userId);
                throw;
            }
        }

        public async Task<LocationReviewDetailResponse> UpdateReviewAsync(Guid reviewId, Guid userId, UpdateLocationReviewRequest request)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                if (review.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this review");
                }

                // Güncelleme
                review.Title = request.Title;
                review.Content = request.Content;
                review.OverallRating = request.OverallRating;
                review.CleanlinessRating = request.CleanlinessRating;
                review.ServiceRating = request.ServiceRating;
                review.LocationRating = request.LocationRating;
                review.ValueRating = request.ValueRating;
                review.FacilitiesRating = request.FacilitiesRating;
                review.IsRecommended = request.IsRecommended;
                review.VisitDate = request.VisitDate;
                review.StayDuration = request.StayDuration;
                review.LastModifiedAt = DateTime.UtcNow;
                review.LastModifiedBy = userId;

                _reviewRepository.Update(review);
                await _reviewRepository.SaveChangesAsync();

                // Fotoğrafları güncelle
                if (request.PhotoIds.Any())
                {
                    await UpdateReviewPhotosAsync(review.Id, request.PhotoIds, userId);
                }

                // Lokasyonun ortalama rating'ini güncelle
                await UpdateLocationAverageRatingAsync(review.LocationId);

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(review.LocationId);

                _logger.LogInformation("Review updated successfully: {ReviewId}", reviewId);

                return await MapToReviewDetailResponse(review, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating review {ReviewId} by user {UserId}", reviewId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteReviewAsync(Guid reviewId, Guid userId)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                if (review.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to delete this review");
                }

                // Soft delete
                review.IsDeleted = true;
                review.DeletedAt = DateTime.UtcNow;
                review.DeletedBy = userId;

                _reviewRepository.Update(review);
                await _reviewRepository.SaveChangesAsync();

                // Lokasyonun ortalama rating'ini güncelle
                await UpdateLocationAverageRatingAsync(review.LocationId);

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(review.LocationId);

                _logger.LogInformation("Review deleted successfully: {ReviewId}", reviewId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review {ReviewId} by user {UserId}", reviewId, userId);
                throw;
            }
        }

        public async Task<bool> MarkReviewHelpfulAsync(Guid reviewId, Guid userId, ReviewHelpfulRequest request)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                // Kullanıcının kendi review'unu helpful olarak işaretlemesini engelle
                if (review.UserId == userId)
                {
                    throw new InvalidOperationException("You cannot mark your own review as helpful");
                }

                var existingHelpful = await _helpfulRepository.SingleOrDefaultAsync(h =>
                    h.ReviewId == reviewId && h.UserId == userId);

                if (existingHelpful != null)
                {
                    // Mevcut işareti güncelle
                    existingHelpful.IsHelpful = request.IsHelpful;
                    _helpfulRepository.Update(existingHelpful);
                }
                else
                {
                    // Yeni işaret ekle
                    var helpful = new ReviewHelpful
                    {
                        Id = Guid.NewGuid(),
                        ReviewId = reviewId,
                        UserId = userId,
                        IsHelpful = request.IsHelpful,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _helpfulRepository.AddAsync(helpful);
                }

                await _helpfulRepository.SaveChangesAsync();

                // Review'un helpful count'larını güncelle
                await UpdateReviewHelpfulCountsAsync(reviewId);

                _logger.LogInformation("Review marked as {Status}: {ReviewId} by user {UserId}",
                    request.IsHelpful ? "helpful" : "not helpful", reviewId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking review helpful: {ReviewId} by user {UserId}", reviewId, userId);
                throw;
            }
        }

        public async Task<bool> RemoveReviewHelpfulAsync(Guid reviewId, Guid userId)
        {
            try
            {
                var helpful = await _helpfulRepository.SingleOrDefaultAsync(h =>
                    h.ReviewId == reviewId && h.UserId == userId);

                if (helpful != null)
                {
                    _helpfulRepository.Remove(helpful);
                    await _helpfulRepository.SaveChangesAsync();

                    // Review'un helpful count'larını güncelle
                    await UpdateReviewHelpfulCountsAsync(reviewId);

                    _logger.LogInformation("Review helpful marking removed: {ReviewId} by user {UserId}", reviewId, userId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing review helpful: {ReviewId} by user {UserId}", reviewId, userId);
                throw;
            }
        }

        public async Task<bool> AddOwnerResponseAsync(Guid reviewId, Guid ownerId, OwnerResponseRequest request)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                var location = await _locationRepository.GetByIdAsync(review.LocationId);

                // Sadece lokasyon sahibi veya admin yanıt verebilir
                if (location.AddedByUserId != ownerId)
                {
                    // TODO: Admin kontrolü eklenebilir
                    throw new UnauthorizedAccessException("You are not authorized to respond to this review");
                }

                review.OwnerResponse = request.Response;
                review.OwnerResponseDate = DateTime.UtcNow;
                review.LastModifiedAt = DateTime.UtcNow;
                review.LastModifiedBy = ownerId;

                _reviewRepository.Update(review);
                await _reviewRepository.SaveChangesAsync();

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(review.LocationId);

                _logger.LogInformation("Owner response added to review: {ReviewId} by owner {OwnerId}", reviewId, ownerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding owner response to review {ReviewId} by owner {OwnerId}", reviewId, ownerId);
                throw;
            }
        }

        public async Task<bool> UpdateOwnerResponseAsync(Guid reviewId, Guid ownerId, OwnerResponseRequest request)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                if (string.IsNullOrEmpty(review.OwnerResponse))
                {
                    throw new InvalidOperationException("No owner response exists for this review");
                }

                var location = await _locationRepository.GetByIdAsync(review.LocationId);

                if (location.AddedByUserId != ownerId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this response");
                }

                review.OwnerResponse = request.Response;
                review.OwnerResponseDate = DateTime.UtcNow;
                review.LastModifiedAt = DateTime.UtcNow;
                review.LastModifiedBy = ownerId;

                _reviewRepository.Update(review);
                await _reviewRepository.SaveChangesAsync();

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(review.LocationId);

                _logger.LogInformation("Owner response updated for review: {ReviewId} by owner {OwnerId}", reviewId, ownerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating owner response for review {ReviewId} by owner {OwnerId}", reviewId, ownerId);
                throw;
            }
        }

        public async Task<bool> RemoveOwnerResponseAsync(Guid reviewId, Guid ownerId)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                var location = await _locationRepository.GetByIdAsync(review.LocationId);

                if (location.AddedByUserId != ownerId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to remove this response");
                }

                review.OwnerResponse = null;
                review.OwnerResponseDate = null;
                review.LastModifiedAt = DateTime.UtcNow;
                review.LastModifiedBy = ownerId;

                _reviewRepository.Update(review);
                await _reviewRepository.SaveChangesAsync();

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(review.LocationId);

                _logger.LogInformation("Owner response removed from review: {ReviewId} by owner {OwnerId}", reviewId, ownerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing owner response from review {ReviewId} by owner {OwnerId}", reviewId, ownerId);
                throw;
            }
        }

        public async Task<LocationRatingBreakdown> GetLocationRatingBreakdownAsync(Guid locationId)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var reviews = await _reviewRepository.FindAsync(r =>
                    r.LocationId == locationId && !r.IsDeleted);

                var reviewList = reviews.ToList();

                if (!reviewList.Any())
                {
                    return new LocationRatingBreakdown
                    {
                        TotalReviews = 0,
                        RatingDistribution = new Dictionary<int, int>()
                    };
                }

                var breakdown = new LocationRatingBreakdown
                {
                    AverageOverall = reviewList.Average(r => (int)r.OverallRating),
                    TotalReviews = reviewList.Count,
                    VerifiedReviews = reviewList.Count(r => r.IsVerified),
                    RecommendedCount = reviewList.Count(r => r.IsRecommended),
                    RatingDistribution = reviewList
                        .GroupBy(r => (int)r.OverallRating)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                // Detaylı puanlamalar
                var cleanlinessRatings = reviewList.Where(r => r.CleanlinessRating.HasValue).ToList();
                if (cleanlinessRatings.Any())
                {
                    breakdown.AverageCleanliness = cleanlinessRatings.Average(r => (int)r.CleanlinessRating.Value);
                }

                var serviceRatings = reviewList.Where(r => r.ServiceRating.HasValue).ToList();
                if (serviceRatings.Any())
                {
                    breakdown.AverageService = serviceRatings.Average(r => (int)r.ServiceRating.Value);
                }

                var locationRatings = reviewList.Where(r => r.LocationRating.HasValue).ToList();
                if (locationRatings.Any())
                {
                    breakdown.AverageLocation = locationRatings.Average(r => (int)r.LocationRating.Value);
                }

                var valueRatings = reviewList.Where(r => r.ValueRating.HasValue).ToList();
                if (valueRatings.Any())
                {
                    breakdown.AverageValue = valueRatings.Average(r => (int)r.ValueRating.Value);
                }

                var facilitiesRatings = reviewList.Where(r => r.FacilitiesRating.HasValue).ToList();
                if (facilitiesRatings.Any())
                {
                    breakdown.AverageFacilities = facilitiesRatings.Average(r => (int)r.FacilitiesRating.Value);
                }

                return breakdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating breakdown for location: {LocationId}", locationId);
                throw;
            }
        }

        public async Task<PagedResponse<LocationReviewSummaryResponse>> GetUserReviewsAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                var reviews = await _reviewRepository.FindAsync(r => r.UserId == userId && !r.IsDeleted);
                var reviewList = reviews.OrderByDescending(r => r.CreatedAt).ToList();

                var totalCount = reviewList.Count;
                var paginatedReviews = reviewList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var responses = new List<LocationReviewSummaryResponse>();
                foreach (var review in paginatedReviews)
                {
                    responses.Add(await MapToReviewSummaryResponse(review, userId));
                }

                return new PagedResponse<LocationReviewSummaryResponse>
                {
                    Items = responses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reviews: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> VerifyReviewAsync(Guid reviewId, Guid adminUserId)
        {
            try
            {
                var review = await _reviewRepository.GetByIdAsync(reviewId);
                if (review == null || review.IsDeleted)
                {
                    throw new KeyNotFoundException($"Review with ID {reviewId} not found");
                }

                review.IsVerified = true;
                review.LastModifiedAt = DateTime.UtcNow;
                review.LastModifiedBy = adminUserId;

                _reviewRepository.Update(review);
                await _reviewRepository.SaveChangesAsync();

                // Cache'i temizle
                await _cacheService.InvalidateLocationCacheAsync(review.LocationId);

                _logger.LogInformation("Review verified: {ReviewId} by admin {AdminUserId}", reviewId, adminUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying review {ReviewId} by admin {AdminUserId}", reviewId, adminUserId);
                throw;
            }
        }

        public async Task<bool> HasUserVisitedLocationAsync(Guid userId, Guid locationId)
        {
            try
            {
                // Basit implementasyon - kullanıcının bu lokasyon için review'u varsa ziyaret etmiş sayılır
                var review = await _reviewRepository.SingleOrDefaultAsync(r =>
                    r.UserId == userId && r.LocationId == locationId && !r.IsDeleted);

                return review != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user visited location: {UserId}, {LocationId}", userId, locationId);
                return false;
            }
        }

        #region Private Helper Methods

        private async Task AttachPhotosToReviewAsync(Guid reviewId, List<Guid> photoIds, Guid userId)
        {
            foreach (var photoId in photoIds)
            {
                var media = await _mediaRepository.GetByIdAsync(photoId);
                if (media != null && !media.IsDeleted)
                {
                    media.EntityId = reviewId;
                    media.EntityType = "Review";
                    media.Status = Domain.Enums.MediaStatus.Active;
                    media.LastModifiedAt = DateTime.UtcNow;
                    media.LastModifiedBy = userId;
                    _mediaRepository.Update(media);
                }
            }
            await _mediaRepository.SaveChangesAsync();
        }

        private async Task UpdateReviewPhotosAsync(Guid reviewId, List<Guid> photoIds, Guid userId)
        {
            // Remove existing photos
            var existingPhotos = await _mediaRepository.FindAsync(m =>
                m.EntityId == reviewId &&
                m.EntityType == "Review" &&
                m.Status == Domain.Enums.MediaStatus.Active &&
                !m.IsDeleted);

            foreach (var photo in existingPhotos)
            {
                photo.IsDeleted = true;
                photo.LastModifiedAt = DateTime.UtcNow;
                photo.LastModifiedBy = userId;
                _mediaRepository.Update(photo);
            }

            // Attach new photos
            await AttachPhotosToReviewAsync(reviewId, photoIds, userId);
        }

        private async Task UpdateLocationAverageRatingAsync(Guid locationId)
        {
            var location = await _locationRepository.GetByIdAsync(locationId);
            if (location == null || location.IsDeleted)
                return;

            var reviews = await _reviewRepository.FindAsync(r => r.LocationId == locationId && !r.IsDeleted);
            var reviewList = reviews.ToList();

            if (reviewList.Any())
            {
                location.AverageRating = reviewList.Average(r => (double)r.OverallRating);
                location.ReviewCount = reviewList.Count;
            }
            else
            {
                location.AverageRating = null;
                location.ReviewCount = 0;
            }

            _locationRepository.Update(location);
            await _locationRepository.SaveChangesAsync();
        }

        private async Task UpdateReviewHelpfulCountsAsync(Guid reviewId)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);
            if (review == null || review.IsDeleted)
                return;

            var helpfuls = await _helpfulRepository.FindAsync(h => h.ReviewId == reviewId);
            review.HelpfulCount = helpfuls.Count(h => h.IsHelpful);
            review.NotHelpfulCount = helpfuls.Count(h => !h.IsHelpful);

            _reviewRepository.Update(review);
            await _reviewRepository.SaveChangesAsync();
        }

        private async Task<LocationReviewSummaryResponse> MapToReviewSummaryResponse(LocationReview review, Guid? currentUserId)
        {
            var user = await _userRepository.GetByIdAsync(review.UserId);

            var response = new LocationReviewSummaryResponse
            {
                Id = review.Id,
                Title = review.Title,
                Content = review.Content,
                OverallRating = (int)review.OverallRating,
                IsVerified = review.IsVerified,
                IsRecommended = review.IsRecommended,
                VisitDate = review.VisitDate,
                StayDuration = review.StayDuration,
                HelpfulCount = review.HelpfulCount,
                NotHelpfulCount = review.NotHelpfulCount,
                CreatedAt = review.CreatedAt,
            };

            // Photos
            var photos = await _mediaRepository.FindAsync(m =>
                m.EntityId == review.Id &&
                m.EntityType == "Review" &&
                m.Status == Domain.Enums.MediaStatus.Active &&
                !m.IsDeleted);

            foreach (var photo in photos.OrderBy(p => p.SortOrder ?? int.MaxValue))
            {
                response.Photos.Add(new MediaSummaryResponse
                {
                    Id = photo.Id,
                    Url = await _mediaService.GenerateSecureUrlAsync(photo.Url),
                    ThumbnailUrl = !string.IsNullOrEmpty(photo.ThumbnailUrl)
                        ? await _mediaService.GenerateSecureUrlAsync(photo.ThumbnailUrl)
                        : null,
                    FileType = photo.FileType,
                    Description = photo.Description,
                    AltTag = photo.AltTag,
                    Width = photo.Width,
                    Height = photo.Height
                });
            }

            // Helpful status for current user
            if (currentUserId.HasValue)
            {
                var helpful = await _helpfulRepository.SingleOrDefaultAsync(h =>
                    h.ReviewId == review.Id && h.UserId == currentUserId.Value);

                if (helpful != null)
                {
                    response.IsHelpfulByCurrentUser = helpful.IsHelpful;
                    response.IsNotHelpfulByCurrentUser = !helpful.IsHelpful;
                }
            }

            return response;
        }

        private async Task<LocationReviewDetailResponse> MapToReviewDetailResponse(LocationReview review, Guid? currentUserId)
        {
            var user = await _userRepository.GetByIdAsync(review.UserId);
            var location = await _locationRepository.GetByIdAsync(review.LocationId);

            var response = new LocationReviewDetailResponse
            {
                Id = review.Id,
                LocationId = review.LocationId,
                LocationName = location?.Name,
                User = new Application.Users.DTOs.UserSummaryResponse
                {
                    Id = user.Id,
                    Name = user.Name,
                    Surname = user.Surname,
                    Username = user.Username,
                    ProfileImageUrl = user.ProfileImageUrl
                },
                Title = review.Title,
                Content = review.Content,
                OverallRating = (int)review.OverallRating,
                CleanlinessRating = review.CleanlinessRating.HasValue ? (int?)review.CleanlinessRating.Value : null,
                ServiceRating = review.ServiceRating.HasValue ? (int?)review.ServiceRating.Value : null,
                LocationRating = review.LocationRating.HasValue ? (int?)review.LocationRating.Value : null,
                ValueRating = review.ValueRating.HasValue ? (int?)review.ValueRating.Value : null,
                FacilitiesRating = review.FacilitiesRating.HasValue ? (int?)review.FacilitiesRating.Value : null,
                IsVerified = review.IsVerified,
                IsRecommended = review.IsRecommended,
                VisitDate = review.VisitDate,
                StayDuration = review.StayDuration,
                HelpfulCount = review.HelpfulCount,
                NotHelpfulCount = review.NotHelpfulCount,
                OwnerResponse = review.OwnerResponse,
                OwnerResponseDate = review.OwnerResponseDate,
                CreatedAt = review.CreatedAt,
                CanEdit = currentUserId == review.UserId,
                CanDelete = currentUserId == review.UserId,
                CanRespond = location?.AddedByUserId == currentUserId
            };

            // Photos
            var photos = await _mediaRepository.FindAsync(m =>
                m.EntityId == review.Id &&
                m.EntityType == "Review" &&
                m.Status == Domain.Enums.MediaStatus.Active &&
                !m.IsDeleted);

            foreach (var photo in photos.OrderBy(p => p.SortOrder ?? int.MaxValue))
            {
                response.Photos.Add(new MediaSummaryResponse
                {
                    Id = photo.Id,
                    Url = await _mediaService.GenerateSecureUrlAsync(photo.Url),
                    ThumbnailUrl = !string.IsNullOrEmpty(photo.ThumbnailUrl)
                        ? await _mediaService.GenerateSecureUrlAsync(photo.ThumbnailUrl)
                        : null,
                    FileType = photo.FileType,
                    Description = photo.Description,
                    AltTag = photo.AltTag,
                    Width = photo.Width,
                    Height = photo.Height
                });
            }

            // Helpful status for current user
            if (currentUserId.HasValue)
            {
                var helpful = await _helpfulRepository.SingleOrDefaultAsync(h =>
                    h.ReviewId == review.Id && h.UserId == currentUserId.Value);

                if (helpful != null)
                {
                    response.IsHelpfulByCurrentUser = helpful.IsHelpful;
                    response.IsNotHelpfulByCurrentUser = !helpful.IsHelpful;
                }
            }

            return response;
        }

        #endregion
    }
}

using Camply.Application.Common.Interfaces;
using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Camply.Application.Media.Interfaces;
using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Camply.Infrastructure.Services
{
    public class LocationService : ILocationService
    {
        private readonly IRepository<Location> _locationRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<LocationBookmark> _bookmarkRepository;
        private readonly IRepository<Media> _mediaRepository;
        private readonly ILocationCacheService _cacheService;
        private readonly ILocationAnalyticsService _analyticsService;
        private readonly IMediaService _mediaService;
        private readonly ILogger<LocationService> _logger;

        public LocationService(
            IRepository<Location> locationRepository,
            IRepository<User> userRepository,
            IRepository<LocationBookmark> bookmarkRepository,
            IRepository<Media> mediaRepository,
            ILocationCacheService cacheService,
            ILocationAnalyticsService analyticsService,
            IMediaService mediaService,
            ILogger<LocationService> logger)
        {
            _locationRepository = locationRepository;
            _userRepository = userRepository;
            _bookmarkRepository = bookmarkRepository;
            _mediaRepository = mediaRepository;
            _cacheService = cacheService;
            _analyticsService = analyticsService;
            _mediaService = mediaService;
            _logger = logger;
        }

        public async Task<LocationDetailResponse> GetLocationByIdAsync(Guid locationId, Guid? currentUserId = null)
        {
            try
            {
                // Cache'den kontrol et
                var cachedLocation = await _cacheService.GetCachedLocationAsync(locationId);
                if (cachedLocation != null)
                {
                    _logger.LogDebug("Location found in cache: {LocationId}", locationId);
                    await _analyticsService.RecordLocationViewAsync(locationId, currentUserId);
                    return cachedLocation;
                }

                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                // Location detaylarını oluştur
                var response = await MapToLocationDetailResponse(location, currentUserId);

                // Cache'e kaydet
                await _cacheService.SetCachedLocationAsync(locationId, response, TimeSpan.FromMinutes(30));

                // Analytics kaydı
                await _analyticsService.RecordLocationViewAsync(locationId, currentUserId);

                _logger.LogInformation("Location retrieved successfully: {LocationId}", locationId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving location: {LocationId}", locationId);
                throw;
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetLocationsAsync(LocationSearchRequest request, Guid? currentUserId = null)
        {
            try
            {
                // Cache key oluştur
                var cacheKey = _cacheService.GenerateSearchCacheKey(request);
                var cachedResults = await _cacheService.GetCachedSearchResultsAsync(cacheKey);

                if (cachedResults != null)
                {
                    _logger.LogDebug("Search results found in cache");
                    return cachedResults;
                }

                var query = BuildLocationQuery(request);

                var totalCount = await CountLocationsAsync(query);

                var locations = await ApplyPagingAndSorting(query, request);

                var responses = new List<LocationSummaryResponse>();
                var userBookmarks = currentUserId.HasValue
                    ? await _cacheService.GetCachedUserBookmarksAsync(currentUserId.Value)
                    : new List<Guid>();

                foreach (var location in locations)
                {
                    var response = await MapToLocationSummaryResponse(location, currentUserId);
                    response.IsBookmarkedByCurrentUser = userBookmarks.Contains(location.Id);
                    responses.Add(response);
                }

                var result = new PagedResponse<LocationSummaryResponse>
                {
                    Items = responses,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
                };

                await _cacheService.SetCachedSearchResultsAsync(cacheKey, result, TimeSpan.FromMinutes(15));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locations with search request");
                throw;
            }
        }

        public async Task<LocationDetailResponse> CreateLocationAsync(Guid userId, CreateLocationRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Koordinat doğrulama
                if (!await VerifyLocationCoordinatesAsync(request.Latitude, request.Longitude, request.Address))
                {
                    throw new ArgumentException("Invalid coordinates or address mismatch");
                }

                var location = new Location
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    Address = request.Address,
                    City = request.City,
                    State = request.State,
                    Country = request.Country,
                    PostalCode = request.PostalCode,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    Type = request.Type,
                    Status = LocationStatus.Pending, // Admin onayı bekliyor
                    AddedByUserId = userId,
                    ContactPhone = request.ContactPhone,
                    ContactEmail = request.ContactEmail,
                    Website = request.Website,
                    OpeningHours = request.OpeningHours,
                    Features = ConvertFeaturesToFlags(request.Features),
                    HasEntryFee = request.HasEntryFee,
                    EntryFee = request.EntryFee,
                    FacebookUrl = request.FacebookUrl,
                    InstagramUrl = request.InstagramUrl,
                    TwitterUrl = request.TwitterUrl,
                    MaxCapacity = request.MaxCapacity,
                    MaxVehicles = request.MaxVehicles,
                    Currency = "TRY",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                await _locationRepository.AddAsync(location);
                await _locationRepository.SaveChangesAsync();

                if (request.PhotoIds.Any())
                {
                    await AttachPhotosToLocationAsync(location.Id, request.PhotoIds, userId);
                }

                // Cache invalidation
                await _cacheService.InvalidateSearchCacheAsync();

                _logger.LogInformation("Location created successfully: {LocationId} by user {UserId}", location.Id, userId);

                return await MapToLocationDetailResponse(location, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location for user {UserId}", userId);
                throw;
            }
        }

        public async Task<LocationDetailResponse> UpdateLocationAsync(Guid locationId, Guid userId, UpdateLocationRequest request)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                // Sadece lokasyonu ekleyen kullanıcı veya admin güncelleyebilir
                if (location.AddedByUserId != userId)
                {
                    // TODO: Admin kontrolü eklenebilir
                    throw new UnauthorizedAccessException("You are not authorized to update this location");
                }

                // Koordinat değişti ise doğrula
                if (location.Latitude != request.Latitude || location.Longitude != request.Longitude)
                {
                    if (!await VerifyLocationCoordinatesAsync(request.Latitude, request.Longitude, request.Address))
                    {
                        throw new ArgumentException("Invalid coordinates or address mismatch");
                    }
                }

                // Güncelleme
                location.Name = request.Name;
                location.Description = request.Description;
                location.Address = request.Address;
                location.City = request.City;
                location.State = request.State;
                location.Country = request.Country;
                location.PostalCode = request.PostalCode;
                location.Latitude = request.Latitude;
                location.Longitude = request.Longitude;
                location.Type = request.Type;
                location.ContactPhone = request.ContactPhone;
                location.ContactEmail = request.ContactEmail;
                location.Website = request.Website;
                location.OpeningHours = request.OpeningHours;
                location.Features = ConvertFeaturesToFlags(request.Features);
                location.HasEntryFee = request.HasEntryFee;
                location.EntryFee = request.EntryFee;
                location.FacebookUrl = request.FacebookUrl;
                location.InstagramUrl = request.InstagramUrl;
                location.TwitterUrl = request.TwitterUrl;
                location.MaxCapacity = request.MaxCapacity;
                location.MaxVehicles = request.MaxVehicles;
                location.LastModifiedAt = DateTime.UtcNow;
                location.LastModifiedBy = userId;

                // Önemli değişiklik varsa tekrar onaya gönder
                if (location.Status == LocationStatus.Active)
                {
                    location.Status = LocationStatus.Pending;
                }

                _locationRepository.Update(location);
                await _locationRepository.SaveChangesAsync();

                // Fotoğrafları güncelle
                if (request.PhotoIds.Any())
                {
                    await UpdateLocationPhotosAsync(location.Id, request.PhotoIds, userId);
                }

                // Cache temizle
                await _cacheService.InvalidateLocationCacheAsync(locationId);
                await _cacheService.InvalidateSearchCacheAsync();

                _logger.LogInformation("Location updated successfully: {LocationId} by user {UserId}", locationId, userId);

                return await MapToLocationDetailResponse(location, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location {LocationId} by user {UserId}", locationId, userId);
                throw;
            }
        }

        public async Task<bool> DeleteLocationAsync(Guid locationId, Guid userId)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                // Sadece lokasyonu ekleyen kullanıcı veya admin silebilir
                if (location.AddedByUserId != userId)
                {
                    // TODO: Admin kontrolü eklenebilir
                    throw new UnauthorizedAccessException("You are not authorized to delete this location");
                }

                // Soft delete
                location.IsDeleted = true;
                location.DeletedAt = DateTime.UtcNow;
                location.DeletedBy = userId;

                _locationRepository.Update(location);
                await _locationRepository.SaveChangesAsync();

                // Cache temizle
                await _cacheService.InvalidateLocationCacheAsync(locationId);
                await _cacheService.InvalidateSearchCacheAsync();

                _logger.LogInformation("Location deleted successfully: {LocationId} by user {UserId}", locationId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location {LocationId} by user {UserId}", locationId, userId);
                throw;
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> SearchLocationsAsync(LocationSearchRequest request, Guid? currentUserId = null)
        {
            return await GetLocationsAsync(request, currentUserId);
        }

        public async Task<LocationFilterOptions> GetFilterOptionsAsync()
        {
            try
            {
                var locations = await _locationRepository.FindAsync(l => l.Status == LocationStatus.Active && !l.IsDeleted);
                var locationList = locations.ToList();

                var filterOptions = new LocationFilterOptions
                {
                    LocationTypes = Enum.GetValues<LocationType>()
                        .Select(type => new LocationTypeOption
                        {
                            Type = type,
                            Name = GetLocationTypeName(type),
                            Count = locationList.Count(l => l.Type == type)
                        }).ToList(),

                    Features = GetAllLocationFeatures()
                        .Select(feature => new FeatureOption
                        {
                            Feature = feature,
                            Name = GetFeatureName(feature),
                            Count = locationList.Count(l => l.HasFeature(GetFeatureFromString(feature))),
                            Icon = GetFeatureIcon(feature)
                        }).ToList(),

                    PriceRange = new PriceRange
                    {
                        MinPrice = locationList.Where(l => l.EntryFee.HasValue).DefaultIfEmpty().Min(l => l.EntryFee ?? 0m),
                        MaxPrice = locationList.Where(l => l.EntryFee.HasValue).DefaultIfEmpty().Max(l => l.EntryFee ?? 0m),
                        Currency = "TRY"
                    },

                    Countries = locationList.Select(l => l.Country).Distinct().OrderBy(c => c).ToList(),
                    Cities = locationList.Select(l => l.City).Distinct().OrderBy(c => c).ToList()
                };

                return filterOptions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filter options");
                throw;
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetLocationsByTypeAsync(LocationType type, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var request = new LocationSearchRequest
            {
                Types = new List<LocationType> { type },
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = "rating"
            };

            return await GetLocationsAsync(request, currentUserId);
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetLocationsByFeatureAsync(string feature, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var request = new LocationSearchRequest
            {
                Features = new List<string> { feature },
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = "rating"
            };

            return await GetLocationsAsync(request, currentUserId);
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetNearbyLocationsAsync(double latitude, double longitude, double radiusKm, int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            var request = new LocationSearchRequest
            {
                Latitude = latitude,
                Longitude = longitude,
                RadiusKm = radiusKm,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = "distance"
            };

            return await GetLocationsAsync(request, currentUserId);
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetSponsoredLocationsAsync(int pageNumber, int pageSize, Guid? currentUserId = null)
        {
            try
            {
                var cachedSponsored = await _cacheService.GetCachedSponsoredLocationsAsync();
                if (cachedSponsored != null && cachedSponsored.Any())
                {
                    var totalCount = cachedSponsored.Count;
                    var paginatedItems = cachedSponsored
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    return new PagedResponse<LocationSummaryResponse>
                    {
                        Items = paginatedItems,
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalCount = totalCount,
                        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                    };
                }

                var request = new LocationSearchRequest
                {
                    IsSponsored = true,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SortBy = "sponsored_priority"
                };

                var result = await GetLocationsAsync(request, currentUserId);

                // Cache'e kaydet
                if (result.Items.Any())
                {
                    await _cacheService.SetCachedSponsoredLocationsAsync(result.Items.ToList(), TimeSpan.FromHours(1));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sponsored locations");
                throw;
            }
        }

        public async Task<bool> SetSponsorshipAsync(Guid locationId, SponsorshipRequest request)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                location.IsSponsored = true;
                location.SponsoredUntil = request.EndDate;
                location.SponsoredPriority = request.Priority;
                location.LastModifiedAt = DateTime.UtcNow;

                _locationRepository.Update(location);
                await _locationRepository.SaveChangesAsync();

                // Sponsored cache temizle
                await _cacheService.InvalidateSponsoredCacheAsync();
                await _cacheService.InvalidateLocationCacheAsync(locationId);

                _logger.LogInformation("Sponsorship set for location: {LocationId}", locationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting sponsorship for location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<bool> RemoveSponsorshipAsync(Guid locationId)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                location.IsSponsored = false;
                location.SponsoredUntil = null;
                location.SponsoredPriority = 0;
                location.LastModifiedAt = DateTime.UtcNow;

                _locationRepository.Update(location);
                await _locationRepository.SaveChangesAsync();

                // Cache temizle
                await _cacheService.InvalidateSponsoredCacheAsync();
                await _cacheService.InvalidateLocationCacheAsync(locationId);

                _logger.LogInformation("Sponsorship removed for location: {LocationId}", locationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing sponsorship for location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetPendingLocationsAsync(int pageNumber, int pageSize)
        {
            try
            {
                var query = await _locationRepository.FindAsync(l => l.Status == LocationStatus.Pending && !l.IsDeleted);
                var locationList = query.OrderBy(l => l.CreatedAt).ToList();

                var totalCount = locationList.Count;
                var paginatedLocations = locationList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var responses = new List<LocationSummaryResponse>();
                foreach (var location in paginatedLocations)
                {
                    responses.Add(await MapToLocationSummaryResponse(location, null));
                }

                return new PagedResponse<LocationSummaryResponse>
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
                _logger.LogError(ex, "Error getting pending locations");
                throw;
            }
        }

        public async Task<bool> ApproveLocationAsync(Guid locationId, Guid adminUserId, AdminLocationApprovalRequest request)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                if (request.IsApproved)
                {
                    location.Status = LocationStatus.Active;
                    location.IsVerified = true;
                    location.ApprovedAt = DateTime.UtcNow;
                    location.ApprovedByUserId = adminUserId;
                }
                else
                {
                    location.Status = LocationStatus.Rejected;
                    location.RejectionReason = request.RejectionReason;
                }

                location.LastModifiedAt = DateTime.UtcNow;
                location.LastModifiedBy = adminUserId;

                _locationRepository.Update(location);
                await _locationRepository.SaveChangesAsync();

                // Cache temizle
                await _cacheService.InvalidateLocationCacheAsync(locationId);
                await _cacheService.InvalidateSearchCacheAsync();

                _logger.LogInformation("Location {Action} by admin {AdminUserId}: {LocationId}",
                    request.IsApproved ? "approved" : "rejected", adminUserId, locationId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing location approval {LocationId} by admin {AdminUserId}", locationId, adminUserId);
                throw;
            }
        }

        public async Task<bool> RejectLocationAsync(Guid locationId, Guid adminUserId, AdminLocationApprovalRequest request)
        {
            request.IsApproved = false;
            return await ApproveLocationAsync(locationId, adminUserId, request);
        }

        public async Task<bool> BookmarkLocationAsync(Guid locationId, Guid userId)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var existingBookmark = await _bookmarkRepository.SingleOrDefaultAsync(b => b.LocationId == locationId && b.UserId == userId);
                if (existingBookmark != null)
                {
                    _logger.LogInformation("Location already bookmarked: {LocationId} by user {UserId}", locationId, userId);
                    return true;
                }

                var bookmark = new LocationBookmark
                {
                    Id = Guid.NewGuid(),
                    LocationId = locationId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _bookmarkRepository.AddAsync(bookmark);
                await _bookmarkRepository.SaveChangesAsync();

                // Cache güncelle
                await _cacheService.AddBookmarkToCacheAsync(userId, locationId);

                _logger.LogInformation("Location bookmarked: {LocationId} by user {UserId}", locationId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bookmarking location {LocationId} by user {UserId}", locationId, userId);
                throw;
            }
        }

        public async Task<bool> UnbookmarkLocationAsync(Guid locationId, Guid userId)
        {
            try
            {
                var bookmark = await _bookmarkRepository.SingleOrDefaultAsync(b => b.LocationId == locationId && b.UserId == userId);
                if (bookmark == null)
                {
                    _logger.LogInformation("Bookmark not found: {LocationId} by user {UserId}", locationId, userId);
                    return true;
                }

                _bookmarkRepository.Remove(bookmark);
                await _bookmarkRepository.SaveChangesAsync();

                // Cache güncelle
                await _cacheService.RemoveBookmarkFromCacheAsync(userId, locationId);

                _logger.LogInformation("Location unbookmarked: {LocationId} by user {UserId}", locationId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbookmarking location {LocationId} by user {UserId}", locationId, userId);
                throw;
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetBookmarkedLocationsAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                var bookmarks = await _bookmarkRepository.FindAsync(b => b.UserId == userId);
                var locationIds = bookmarks.Select(b => b.LocationId).ToList();

                if (!locationIds.Any())
                {
                    return new PagedResponse<LocationSummaryResponse>
                    {
                        Items = new List<LocationSummaryResponse>(),
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalCount = 0,
                        TotalPages = 0
                    };
                }

                var locations = await _locationRepository.FindAsync(l =>
                    locationIds.Contains(l.Id) &&
                    l.Status == LocationStatus.Active &&
                    !l.IsDeleted);

                var locationList = locations.OrderByDescending(l => l.CreatedAt).ToList();
                var totalCount = locationList.Count;

                var paginatedLocations = locationList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var responses = new List<LocationSummaryResponse>();
                foreach (var location in paginatedLocations)
                {
                    var response = await MapToLocationSummaryResponse(location, userId);
                    response.IsBookmarkedByCurrentUser = true;
                    responses.Add(response);
                }

                return new PagedResponse<LocationSummaryResponse>
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
                _logger.LogError(ex, "Error getting bookmarked locations for user {UserId}", userId);
                throw;
            }
        }

        public async Task<LocationStatisticsResponse> GetLocationStatisticsAsync(Guid locationId)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null || location.IsDeleted)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var viewStats = await _analyticsService.GetLocationViewStatsAsync(locationId);
                var monthlyViews = await _analyticsService.GetMonthlyViewStatsAsync(locationId, 6);

                return new LocationStatisticsResponse
                {
                    TotalViews = viewStats.TotalViews,
                    TotalReviews = location.ReviewCount,
                    AverageRating = location.AverageRating ?? 0,
                    MonthlyViews = monthlyViews,
                    BookmarkCount = await GetBookmarkCountAsync(locationId),
                    ShareCount = 0 // TODO: Share tracking eklenebilir
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location statistics for {LocationId}", locationId);
                throw;
            }
        }

        public async Task IncrementViewCountAsync(Guid locationId, Guid? userId = null)
        {
            await _analyticsService.RecordLocationViewAsync(locationId, userId);
        }

        public async Task<bool> VerifyLocationCoordinatesAsync(double latitude, double longitude, string address)
        {
            try
            {
                // Basit validasyon - daha gelişmiş geocoding servisi entegre edilebilir
                if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                {
                    return false;
                }

                // TODO: Gerçek geocoding API entegrasyonu
                // Örnek: Google Maps Geocoding API, Azure Maps vb.

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying coordinates: {Latitude}, {Longitude}", latitude, longitude);
                return false;
            }
        }

        #region Private Helper Methods

        private async Task<LocationDetailResponse> MapToLocationDetailResponse(Location location, Guid? currentUserId)
        {
            var response = new LocationDetailResponse
            {
                Id = location.Id,
                Name = location.Name,
                Description = location.Description,
                Address = location.Address,
                City = location.City,
                Country = location.Country,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Type = GetLocationTypeName(location.Type),
                Status = location.Status.ToString(),
                IsSponsored = location.IsSponsoredAndActive,
                SponsoredPriority = location.SponsoredPriority,
                HasEntryFee = location.HasEntryFee,
                EntryFee = location.EntryFee,
                Currency = location.Currency,
                AverageRating = location.AverageRating,
                ReviewCount = location.ReviewCount,
                ContactPhone = location.ContactPhone,
                ContactEmail = location.ContactEmail,
                Website = location.Website,
                OpeningHours = location.OpeningHours,
                AllFeatures = GetLocationFeaturesList(location.Features),
                MaxCapacity = location.MaxCapacity,
                MaxVehicles = location.MaxVehicles,
                FacebookUrl = location.FacebookUrl,
                InstagramUrl = location.InstagramUrl,
                TwitterUrl = location.TwitterUrl,
                ApprovedAt = location.ApprovedAt,
                TotalVisitCount = location.TotalVisitCount,
                CreatedAt = location.CreatedAt
            };

            // Added by user
            if (location.AddedByUserId.HasValue)
            {
                var addedByUser = await _userRepository.GetByIdAsync(location.AddedByUserId.Value);
                if (addedByUser != null)
                {
                    response.AddedBy = new Application.Users.DTOs.UserSummaryResponse
                    {
                        Id = addedByUser.Id,
                        Name = addedByUser.Name,
                        Surname = addedByUser.Surname,
                        Username = addedByUser.Username,
                        ProfileImageUrl = addedByUser.ProfileImageUrl
                    };
                }
            }

            // Photos
            var photos = await _mediaRepository.FindAsync(m =>
                m.EntityId == location.Id &&
                m.EntityType == "Location" &&
                m.Status == MediaStatus.Active &&
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

            // Primary image
            response.PrimaryImageUrl = response.Photos.FirstOrDefault()?.Url;

            // Bookmark status
            if (currentUserId.HasValue)
            {
                var userBookmarks = await _cacheService.GetCachedUserBookmarksAsync(currentUserId.Value);
                response.IsBookmarkedByCurrentUser = userBookmarks.Contains(location.Id);
            }

            return response;
        }

        private async Task<LocationSummaryResponse> MapToLocationSummaryResponse(Location location, Guid? currentUserId)
        {
            var response = new LocationSummaryResponse
            {
                Id = location.Id,
                Name = location.Name,
                Description = location.Description?.Length > 200 ? location.Description.Substring(0, 200) + "..." : location.Description,
                Address = location.Address,
                City = location.City,
                Country = location.Country,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Type = GetLocationTypeName(location.Type),
                Status = location.Status.ToString(),
                IsSponsored = location.IsSponsoredAndActive,
                SponsoredPriority = location.SponsoredPriority,
                HasEntryFee = location.HasEntryFee,
                EntryFee = location.EntryFee,
                Currency = location.Currency,
                AverageRating = location.AverageRating,
                ReviewCount = location.ReviewCount,
                MainFeatures = GetMainFeatures(location.Features),
                CreatedAt = location.CreatedAt
            };

            // Primary image
            var primaryPhoto = await _mediaRepository.FindAsync(m =>
                m.EntityId == location.Id &&
                m.EntityType == "Location" &&
                m.Status == MediaStatus.Active &&
                !m.IsDeleted);

            if (primaryPhoto.Any())
            {
                response.PrimaryImageUrl = await _mediaService.GenerateSecureUrlAsync(primaryPhoto.FirstOrDefault().ThumbnailUrl ?? primaryPhoto.FirstOrDefault().Url);
            }

            // Distance calculation (if coordinates provided in search)
            // This would be calculated in the query if needed

            return response;
        }

        private IQueryable<Location> BuildLocationQuery(LocationSearchRequest request)
        {
            var locations = _locationRepository.FindAsync(l =>
                (l.Status == LocationStatus.Pending || l.Status == LocationStatus.Active) && !l.IsDeleted).Result.AsQueryable();

            // Text search
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var searchTerm = request.Query.ToLower();
                locations = locations.Where(l =>
                    l.Name.ToLower().Contains(searchTerm) ||
                    l.Description.ToLower().Contains(searchTerm) ||
                    l.City.ToLower().Contains(searchTerm) ||
                    l.Country.ToLower().Contains(searchTerm));
            }

            // Location type filter
            if (request.Types?.Any() == true)
            {
                locations = locations.Where(l => request.Types.Contains(l.Type));
            }

            // Features filter
            if (request.Features?.Any() == true)
            {
                foreach (var feature in request.Features)
                {
                    var featureFlag = GetFeatureFromString(feature);
                    locations = locations.Where(l => l.Features.HasFlag(featureFlag));
                }
            }

            // Price range filter
            if (request.MinPrice.HasValue)
            {
                locations = locations.Where(l => !l.EntryFee.HasValue || l.EntryFee >= request.MinPrice);
            }

            if (request.MaxPrice.HasValue)
            {
                locations = locations.Where(l => !l.EntryFee.HasValue || l.EntryFee <= request.MaxPrice);
            }

            // Rating filter
            if (request.MinRating.HasValue)
            {
                locations = locations.Where(l => l.AverageRating >= request.MinRating);
            }

            // Sponsored filter
            if (request.IsSponsored.HasValue)
            {
                if (request.IsSponsored.Value)
                {
                    locations = locations.Where(l => l.IsSponsored && l.SponsoredUntil > DateTime.UtcNow);
                }
                else
                {
                    locations = locations.Where(l => !l.IsSponsored || l.SponsoredUntil <= DateTime.UtcNow);
                }
            }

            // Entry fee filter
            if (request.HasEntryFee.HasValue)
            {
                locations = locations.Where(l => l.HasEntryFee == request.HasEntryFee.Value);
            }

            // Coordinate-based filtering (nearby)
            if (request.Latitude.HasValue && request.Longitude.HasValue && request.RadiusKm.HasValue)
            {
                // Basit distance calculation - production'da daha optimize edilmeli
                var radiusInDegrees = request.RadiusKm.Value / 111.0; // Approximate conversion
                locations = locations.Where(l =>
                    Math.Abs(l.Latitude - request.Latitude.Value) <= radiusInDegrees &&
                    Math.Abs(l.Longitude - request.Longitude.Value) <= radiusInDegrees);
            }

            return locations;
        }

        private async Task<int> CountLocationsAsync(IQueryable<Location> query)
        {
            return query.Count();
        }

        private async Task<List<Location>> ApplyPagingAndSorting(IQueryable<Location> query, LocationSearchRequest request)
        {
            // Sorting
            query = request.SortBy?.ToLower() switch
            {
                "rating" => query.OrderByDescending(l => l.AverageRating ?? 0).ThenByDescending(l => l.ReviewCount),
                "price" => query.OrderBy(l => l.EntryFee ?? 0),
                "name" => query.OrderBy(l => l.Name),
                "created" => query.OrderByDescending(l => l.CreatedAt),
                "popular" => query.OrderByDescending(l => l.TotalVisitCount).ThenByDescending(l => l.ReviewCount),
                "sponsored_priority" => query.OrderByDescending(l => l.IsSponsored ? l.SponsoredPriority : 0).ThenByDescending(l => l.AverageRating ?? 0),
                _ => query.OrderByDescending(l => l.IsSponsored ? l.SponsoredPriority : 0).ThenByDescending(l => l.CreatedAt)
            };

            // Paging
            return query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();
        }

        private LocationFeature ConvertFeaturesToFlags(List<string> features)
        {
            var result = LocationFeature.None;

            foreach (var feature in features ?? new List<string>())
            {
                var featureFlag = GetFeatureFromString(feature);
                result |= featureFlag;
            }

            return result;
        }

        private LocationFeature GetFeatureFromString(string feature)
        {
            return feature?.ToLower() switch
            {
                "shower" => LocationFeature.Shower,
                "toilet" => LocationFeature.Toilet,
                "restaurant" => LocationFeature.Restaurant,
                "market" => LocationFeature.Market,
                "cafe" => LocationFeature.Cafe,
                "bar" => LocationFeature.Bar,
                "wifi" => LocationFeature.WiFi,
                "cellsignal" => LocationFeature.CellSignal,
                "security" => LocationFeature.Security,
                "safebox" => LocationFeature.SafeBox,
                "lighting" => LocationFeature.Lighting,
                "electricity" => LocationFeature.Electricity,
                "watersupply" => LocationFeature.WaterSupply,
                "hotwater" => LocationFeature.HotWater,
                "laundry" => LocationFeature.Laundry,
                "cleaning" => LocationFeature.Cleaning,
                "parking" => LocationFeature.Parking,
                "publictransport" => LocationFeature.PublicTransport,
                "carrental" => LocationFeature.CarRental,
                "pool" => LocationFeature.Pool,
                "playground" => LocationFeature.Playground,
                "sports" => LocationFeature.Sports,
                "bikerental" => LocationFeature.BikeRental,
                "beachaccess" => LocationFeature.BeachAccess,
                "hikingtrails" => LocationFeature.HikingTrails,
                "natureview" => LocationFeature.NatureView,
                "petfriendly" => LocationFeature.PetFriendly,
                "familyfriendly" => LocationFeature.FamilyFriendly,
                "babyfacilities" => LocationFeature.BabyFacilities,
                "kitchen" => LocationFeature.Kitchen,
                "barbecue" => LocationFeature.Barbecue,
                "minibar" => LocationFeature.Minibar,
                _ => LocationFeature.None
            };
        }

        private List<string> GetLocationFeaturesList(LocationFeature features)
        {
            var result = new List<string>();

            foreach (LocationFeature feature in Enum.GetValues<LocationFeature>())
            {
                if (feature != LocationFeature.None && features.HasFlag(feature))
                {
                    result.Add(GetFeatureStringFromEnum(feature));
                }
            }

            return result;
        }

        private List<string> GetMainFeatures(LocationFeature features)
        {
            var mainFeatures = new List<LocationFeature>
            {
                LocationFeature.WiFi,
                LocationFeature.Parking,
                LocationFeature.Restaurant,
                LocationFeature.Pool,
                LocationFeature.PetFriendly,
                LocationFeature.BeachAccess
            };
            return mainFeatures
                .Where(feature => features.HasFlag(feature)) 
                .Take(3)
                .Select(GetFeatureStringFromEnum)
                .ToList();
        
        }

        private string GetFeatureStringFromEnum(LocationFeature feature)
        {
            return feature.ToString().ToLower();
        }

        private List<string> GetAllLocationFeatures()
        {
            return Enum.GetValues<LocationFeature>()
                .Where(f => f != LocationFeature.None)
                .Select(f => f.ToString().ToLower())
                .ToList();
        }

        private string GetLocationTypeName(LocationType type)
        {
            return type switch
            {
                LocationType.CampGround => "Kamp Alanı",
                LocationType.NationalPark => "Milli Park",
                LocationType.HikingTrail => "Yürüyüş Rotası",
                LocationType.RVPark => "Karavan Parkı",
                LocationType.BeachCamp => "Plaj Kampı",
                LocationType.ForestCamp => "Orman Kampı",
                LocationType.MountainCamp => "Dağ Kampı",
                LocationType.Hotel => "Otel",
                LocationType.Motel => "Motel",
                LocationType.Hostel => "Hostel",
                LocationType.Resort => "Resort",
                LocationType.GuestHouse => "Pansiyon",
                LocationType.BedAndBreakfast => "Oda & Kahvaltı",
                LocationType.Apartment => "Apart",
                LocationType.Villa => "Villa",
                LocationType.Restaurant => "Restoran",
                LocationType.Cafe => "Kafe",
                LocationType.TouristAttraction => "Turistik Mekan",
                LocationType.Museum => "Müze",
                LocationType.Park => "Park",
                LocationType.Beach => "Plaj",
                LocationType.Lake => "Göl",
                LocationType.River => "Nehir",
                LocationType.Waterfall => "Şelale",
                LocationType.Cave => "Mağara",
                LocationType.Mountain => "Dağ",
                LocationType.Forest => "Orman",
                LocationType.Desert => "Çöl",
                LocationType.Island => "Ada",
                LocationType.Valley => "Vadi",
                LocationType.Canyon => "Kanyon",
                _ => "Diğer"
            };
        }

        private string GetFeatureName(string feature)
        {
            return feature?.ToLower() switch
            {
                "shower" => "Duş",
                "toilet" => "WC",
                "restaurant" => "Restoran",
                "market" => "Market",
                "cafe" => "Kafe",
                "bar" => "Bar",
                "wifi" => "WiFi",
                "cellsignal" => "Cep Telefonu Sinyali",
                "security" => "Güvenlik",
                "safebox" => "Kasa",
                "lighting" => "Aydınlatma",
                "electricity" => "Elektrik",
                "watersupply" => "Su Temini",
                "hotwater" => "Sıcak Su",
                "laundry" => "Çamaşırhane",
                "cleaning" => "Temizlik Servisi",
                "parking" => "Otopark",
                "publictransport" => "Toplu Taşıma",
                "carrental" => "Araç Kiralama",
                "pool" => "Havuz",
                "playground" => "Oyun Alanı",
                "sports" => "Spor Tesisleri",
                "bikerental" => "Bisiklet Kiralama",
                "beachaccess" => "Plaj Erişimi",
                "hikingtrails" => "Yürüyüş Yolları",
                "natureview" => "Doğa Manzarası",
                "petfriendly" => "Evcil Hayvan Dostu",
                "familyfriendly" => "Aile Dostu",
                "babyfacilities" => "Bebek Tesisleri",
                "kitchen" => "Mutfak",
                "barbecue" => "Barbekü",
                "minibar" => "Minibar",
                _ => feature?.ToUpper()
            };
        }

        private string GetFeatureIcon(string feature)
        {
            return feature?.ToLower() switch
            {
                "shower" => "🚿",
                "toilet" => "🚽",
                "restaurant" => "🍽️",
                "market" => "🛒",
                "cafe" => "☕",
                "bar" => "🍺",
                "wifi" => "📶",
                "cellsignal" => "📱",
                "security" => "🔒",
                "safebox" => "🔐",
                "lighting" => "💡",
                "electricity" => "⚡",
                "watersupply" => "💧",
                "hotwater" => "🔥",
                "laundry" => "👕",
                "cleaning" => "🧹",
                "parking" => "🅿️",
                "publictransport" => "🚌",
                "carrental" => "🚗",
                "pool" => "🏊",
                "playground" => "🎪",
                "sports" => "⚽",
                "bikerental" => "🚴",
                "beachaccess" => "🏖️",
                "hikingtrails" => "🥾",
                "natureview" => "🌲",
                "petfriendly" => "🐕",
                "familyfriendly" => "👨‍👩‍👧‍👦",
                "babyfacilities" => "👶",
                "kitchen" => "🍳",
                "barbecue" => "🔥",
                "minibar" => "🍷",
                _ => "📍"
            };
        }

        private async Task AttachPhotosToLocationAsync(Guid locationId, List<Guid> photoIds, Guid userId)
        {
            try
            {
                for (int i = 0; i < photoIds.Count; i++)
                {
                    await _mediaService.AssignMediaToEntityAsync(photoIds[i], locationId, "Location", userId);

                    var media = await _mediaRepository.GetByIdAsync(photoIds[i]);
                    if (media != null)
                    {
                        media.SortOrder = i;
                        _mediaRepository.Update(media);
                    }
                }

                await _mediaRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching photos to location {LocationId}", locationId);
                throw;
            }
        }

        private async Task UpdateLocationPhotosAsync(Guid locationId, List<Guid> photoIds, Guid userId)
        {
            try
            {
                // Eski fotoğrafları çıkar
                var existingPhotos = await _mediaRepository.FindAsync(m =>
                    m.EntityId == locationId &&
                    m.EntityType == "Location");

                foreach (var photo in existingPhotos)
                {
                    await _mediaService.UnassignMediaFromEntityAsync(photo.Id, userId);
                }

                // Yeni fotoğrafları ekle
                await AttachPhotosToLocationAsync(locationId, photoIds, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location photos for {LocationId}", locationId);
                throw;
            }
        }

        private async Task<int> GetBookmarkCountAsync(Guid locationId)
        {
            try
            {
                var bookmarks = await _bookmarkRepository.FindAsync(b => b.LocationId == locationId);
                return bookmarks.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bookmark count for location {LocationId}", locationId);
                return 0;
            }
        }

        #endregion
    }
}
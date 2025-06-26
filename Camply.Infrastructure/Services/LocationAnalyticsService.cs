using Camply.Application.Common.Models;
using Camply.Application.Locations.DTOs;
using Camply.Application.Locations.Interfaces;
using Camply.Domain;
using Camply.Domain.Analytics;
using Camply.Domain.Repositories;
using Camply.Domain.Enums;
using Camply.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Camply.Infrastructure.Services
{
    public class LocationAnalyticsService : ILocationAnalyticsService
    {
        private readonly MongoDbContext _mongoContext;
        private readonly IRepository<Location> _locationRepository;
        private readonly IRepository<LocationReview> _reviewRepository;
        private readonly IRepository<LocationBookmark> _bookmarkRepository;
        private readonly ILogger<LocationAnalyticsService> _logger;

        // MongoDB Collections
        private IMongoCollection<LocationView> LocationViews => _mongoContext.Database.GetCollection<LocationView>("LocationViews");
        private IMongoCollection<LocationInteraction> LocationInteractions => _mongoContext.Database.GetCollection<LocationInteraction>("LocationInteractions");
        private IMongoCollection<LocationPopularityMetrics> PopularityMetrics => _mongoContext.Database.GetCollection<LocationPopularityMetrics>("LocationPopularityMetrics");
        private IMongoCollection<LocationSearchMetrics> SearchMetrics => _mongoContext.Database.GetCollection<LocationSearchMetrics>("LocationSearchMetrics");

        public LocationAnalyticsService(
            MongoDbContext mongoContext,
            IRepository<Location> locationRepository,
            IRepository<LocationReview> reviewRepository,
            IRepository<LocationBookmark> bookmarkRepository,
            ILogger<LocationAnalyticsService> logger)
        {
            _mongoContext = mongoContext;
            _locationRepository = locationRepository;
            _reviewRepository = reviewRepository;
            _bookmarkRepository = bookmarkRepository;
            _logger = logger;
        }

        #region View Tracking

        public async Task RecordLocationViewAsync(Guid locationId, Guid? userId, string ipAddress = null)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var sessionId = GenerateSessionId(userId, ipAddress);

                // Bugün aynı session'dan view var mı kontrol et
                var existingView = await LocationViews.Find(v =>
                    v.LocationId == locationId &&
                    v.SessionId == sessionId &&
                    v.ViewedAt >= today &&
                    v.ViewedAt < today.AddDays(1)).FirstOrDefaultAsync();

                var isUniqueView = existingView == null;

                var locationView = new LocationView
                {
                    LocationId = locationId,
                    UserId = userId,
                    IpAddress = ipAddress,
                    ViewedAt = DateTime.UtcNow,
                    SessionId = sessionId,
                    IsUniqueView = isUniqueView,
                    DeviceType = "Unknown", // TODO: User-Agent parse
                    Platform = "Unknown"
                };

                await LocationViews.InsertOneAsync(locationView);

                // Daily metrics güncelle
                await UpdateDailyMetricsAsync(locationId, today);

                _logger.LogDebug("Location view recorded: {LocationId} by {UserId}", locationId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording location view: {LocationId}", locationId);
            }
        }

        public async Task<LocationViewStats> GetLocationViewStatsAsync(Guid locationId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var filter = Builders<LocationView>.Filter.And(
                    Builders<LocationView>.Filter.Eq(v => v.LocationId, locationId),
                    Builders<LocationView>.Filter.Gte(v => v.ViewedAt, start),
                    Builders<LocationView>.Filter.Lte(v => v.ViewedAt, end)
                );

                var views = await LocationViews.Find(filter).ToListAsync();

                var today = DateTime.UtcNow.Date;
                var thisWeek = today.AddDays(-7);
                var thisMonth = today.AddDays(-30);

                var stats = new LocationViewStats
                {
                    LocationId = locationId,
                    TotalViews = views.Count,
                    UniqueViews = views.Count(v => v.IsUniqueView),
                    ViewsToday = views.Count(v => v.ViewedAt >= today),
                    ViewsThisWeek = views.Count(v => v.ViewedAt >= thisWeek),
                    ViewsThisMonth = views.Count(v => v.ViewedAt >= thisMonth),
                    LastViewDate = views.OrderByDescending(v => v.ViewedAt).FirstOrDefault()?.ViewedAt ?? DateTime.MinValue
                };

                // Daily breakdown
                stats.DailyViews = views
                    .GroupBy(v => v.ViewedAt.Date)
                    .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count());

                // Hourly breakdown for today
                var todayViews = views.Where(v => v.ViewedAt >= today);
                stats.HourlyViews = todayViews
                    .GroupBy(v => v.ViewedAt.Hour)
                    .ToDictionary(g => g.Key.ToString("D2"), g => g.Count());

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location view stats: {LocationId}", locationId);
                return new LocationViewStats { LocationId = locationId };
            }
        }

        public async Task<Dictionary<string, int>> GetMonthlyViewStatsAsync(Guid locationId, int months = 12)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddMonths(-months);
                var filter = Builders<LocationView>.Filter.And(
                    Builders<LocationView>.Filter.Eq(v => v.LocationId, locationId),
                    Builders<LocationView>.Filter.Gte(v => v.ViewedAt, startDate)
                );

                var views = await LocationViews.Find(filter).ToListAsync();

                return views
                    .GroupBy(v => v.ViewedAt.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly view stats: {LocationId}", locationId);
                return new Dictionary<string, int>();
            }
        }

        #endregion

        #region Popularity Analysis

        public async Task<PagedResponse<LocationSummaryResponse>> GetTrendingLocationsAsync(int pageNumber, int pageSize, TimeSpan? timeRange = null)
        {
            try
            {
                var days = timeRange?.Days ?? 7;
                var startDate = DateTime.UtcNow.AddDays(-days);

                var pipeline = new[]
                {
                    new BsonDocument("$match", new BsonDocument
                    {
                        { "viewedAt", new BsonDocument("$gte", startDate) }
                    }),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", "$locationId" },
                        { "viewCount", new BsonDocument("$sum", 1) },
                        { "uniqueViews", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$isUniqueView", 1, 0 })) }
                    }),
                    new BsonDocument("$sort", new BsonDocument("viewCount", -1)),
                    new BsonDocument("$skip", (pageNumber - 1) * pageSize),
                    new BsonDocument("$limit", pageSize)
                };

                var trendingLocationIds = await LocationViews.Aggregate<BsonDocument>(pipeline).ToListAsync();
                var locationIds = trendingLocationIds.Select(doc => Guid.Parse(doc["_id"].AsString)).ToList();

                var locations = await _locationRepository.FindAsync(l => locationIds.Contains(l.Id) && !l.IsDeleted);
                var locationResponses = await MapToLocationSummaryResponsesAsync(locations.ToList());

                // View count'a göre sırala
                var orderedResponses = locationResponses.OrderByDescending(l =>
                    trendingLocationIds.FirstOrDefault(t => Guid.Parse(t["_id"].AsString) == l.Id)?["viewCount"]?.AsInt32 ?? 0
                ).ToList();

                var totalTrendingCount = await LocationViews.CountDocumentsAsync(
                    Builders<LocationView>.Filter.Gte(v => v.ViewedAt, startDate));

                return new PagedResponse<LocationSummaryResponse>
                {
                    Items = orderedResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = (int)totalTrendingCount,
                    TotalPages = (int)Math.Ceiling(totalTrendingCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trending locations");
                return new PagedResponse<LocationSummaryResponse> { Items = new List<LocationSummaryResponse>() };
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetMostReviewedLocationsAsync(int pageNumber, int pageSize, TimeSpan? timeRange = null)
        {
            try
            {
                var startDate = timeRange.HasValue ? DateTime.UtcNow.Subtract(timeRange.Value) : DateTime.UtcNow.AddDays(-30);

                var recentReviews = await _reviewRepository.FindAsync(r =>
                    r.CreatedAt >= startDate && !r.IsDeleted);

                var locationReviewCounts = recentReviews
                    .GroupBy(r => r.LocationId)
                    .OrderByDescending(g => g.Count())
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToDictionary(g => g.Key, g => g.Count());

                var locations = await _locationRepository.FindAsync(l =>
                    locationReviewCounts.Keys.Contains(l.Id) && !l.IsDeleted);

                var locationResponses = await MapToLocationSummaryResponsesAsync(locations.ToList());

                // Review count'a göre sırala
                var orderedResponses = locationResponses
                    .OrderByDescending(l => locationReviewCounts.GetValueOrDefault(l.Id, 0))
                    .ToList();

                var totalCount = recentReviews.GroupBy(r => r.LocationId).Count();

                return new PagedResponse<LocationSummaryResponse>
                {
                    Items = orderedResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most reviewed locations");
                return new PagedResponse<LocationSummaryResponse> { Items = new List<LocationSummaryResponse>() };
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetHighestRatedLocationsAsync(int pageNumber, int pageSize, double minRating = 4.0)
        {
            try
            {
                var locations = await _locationRepository.FindAsync(l =>
                    l.AverageRating >= minRating &&
                    l.ReviewCount >= 5 && // En az 5 review olsun
                    !l.IsDeleted);

                var locationList = locations
                    .OrderByDescending(l => l.AverageRating)
                    .ThenByDescending(l => l.ReviewCount)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var locationResponses = await MapToLocationSummaryResponsesAsync(locationList);

                var totalCount = locations.Count();

                return new PagedResponse<LocationSummaryResponse>
                {
                    Items = locationResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting highest rated locations");
                return new PagedResponse<LocationSummaryResponse> { Items = new List<LocationSummaryResponse>() };
            }
        }

        #endregion

        #region Regional Statistics

        public async Task<Dictionary<string, int>> GetLocationStatsByRegionAsync(string country = null, string city = null)
        {
            try
            {
                var locations = await _locationRepository.FindAsync(l =>
                    l.Status == LocationStatus.Active && !l.IsDeleted);

                if (!string.IsNullOrEmpty(country))
                {
                    locations = locations.Where(l => l.Country == country);
                }

                if (!string.IsNullOrEmpty(city))
                {
                    locations = locations.Where(l => l.City == city);
                    return locations.GroupBy(l => l.Address).ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
                }
                else if (!string.IsNullOrEmpty(country))
                {
                    return locations.GroupBy(l => l.City).ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
                }
                else
                {
                    return locations.GroupBy(l => l.Country).ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location stats by region");
                return new Dictionary<string, int>();
            }
        }

        public async Task<Dictionary<LocationType, int>> GetLocationStatsByTypeAsync()
        {
            try
            {
                var locations = await _locationRepository.FindAsync(l =>
                    l.Status == LocationStatus.Active && !l.IsDeleted);

                return locations
                    .GroupBy(l => l.Type)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location stats by type");
                return new Dictionary<LocationType, int>();
            }
        }

        public async Task<Dictionary<string, int>> GetPopularFeaturesAsync()
        {
            try
            {
                var locations = await _locationRepository.FindAsync(l =>
                    l.Status == LocationStatus.Active && !l.IsDeleted);

                var featureCounts = new Dictionary<string, int>();

                foreach (var location in locations)
                {
                    foreach (LocationFeature feature in Enum.GetValues<LocationFeature>())
                    {
                        if (feature != LocationFeature.None && location.HasFeature(feature))
                        {
                            var featureName = feature.ToString();
                            featureCounts[featureName] = featureCounts.GetValueOrDefault(featureName, 0) + 1;
                        }
                    }
                }

                return featureCounts.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular features");
                return new Dictionary<string, int>();
            }
        }

        #endregion

        #region User Behavior Analysis

        public async Task<Dictionary<string, object>> GetUserLocationInteractionStatsAsync(Guid userId)
        {
            try
            {
                var stats = new Dictionary<string, object>();

                // View history
                var userViews = await LocationViews.Find(v => v.UserId == userId).ToListAsync();
                stats["TotalViews"] = userViews.Count;
                stats["UniqueLocationsViewed"] = userViews.Select(v => v.LocationId).Distinct().Count();

                // Recent activity
                var last30Days = DateTime.UtcNow.AddDays(-30);
                var recentViews = userViews.Where(v => v.ViewedAt >= last30Days).ToList();
                stats["RecentViews"] = recentViews.Count;
                stats["ViewsLast30Days"] = recentViews.Count;

                // Bookmarks
                var bookmarks = await _bookmarkRepository.FindAsync(b => b.UserId == userId);
                stats["BookmarkCount"] = bookmarks.Count();

                // Reviews
                var reviews = await _reviewRepository.FindAsync(r => r.UserId == userId);
                stats["ReviewCount"] = reviews.Count();
                stats["AverageRatingGiven"] = reviews.Any() ? reviews.Average(r => (int)r.OverallRating) : 0;

                // Interactions
                var interactions = await LocationInteractions.Find(i => i.UserId == userId).ToListAsync();
                stats["TotalInteractions"] = interactions.Count;

                // Preferred location types
                var viewedLocationIds = userViews.Select(v => v.LocationId).Distinct().ToList();
                var viewedLocations = await _locationRepository.FindAsync(l => viewedLocationIds.Contains(l.Id));
                var preferredTypes = viewedLocations
                    .GroupBy(l => l.Type)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
                stats["PreferredLocationTypes"] = preferredTypes;

                // Activity by time
                var activityByHour = userViews
                    .GroupBy(v => v.ViewedAt.Hour)
                    .ToDictionary(g => g.Key, g => g.Count());
                stats["ActivityByHour"] = activityByHour;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user interaction stats: {UserId}", userId);
                return new Dictionary<string, object>();
            }
        }

        public async Task<PagedResponse<LocationSummaryResponse>> GetRecommendedLocationsAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                // Basit recommendation algorithm
                // 1. Kullanıcının daha önce baktığı lokasyon tiplerini al
                // 2. Benzer özelliklere sahip yeni lokasyonları öner

                var userViews = await LocationViews.Find(v => v.UserId == userId).ToListAsync();
                var viewedLocationIds = userViews.Select(v => v.LocationId).Distinct().ToList();

                if (!viewedLocationIds.Any())
                {
                    // Yeni kullanıcı için popüler lokasyonları öner
                    return await GetTrendingLocationsAsync(pageNumber, pageSize, TimeSpan.FromDays(30));
                }

                var viewedLocations = await _locationRepository.FindAsync(l => viewedLocationIds.Contains(l.Id));

                // En çok bakılan lokasyon tiplerini bul
                var preferredTypes = viewedLocations
                    .GroupBy(l => l.Type)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();

                // Henüz bakmadığı benzer lokasyonları öner
                var recommendations = await _locationRepository.FindAsync(l =>
                    preferredTypes.Contains(l.Type) &&
                    !viewedLocationIds.Contains(l.Id) &&
                    l.Status == LocationStatus.Active &&
                    !l.IsDeleted);

                var recommendationList = recommendations
                    .OrderByDescending(l => l.AverageRating ?? 0)
                    .ThenByDescending(l => l.ReviewCount)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var locationResponses = await MapToLocationSummaryResponsesAsync(recommendationList);
                var totalCount = recommendations.Count();

                return new PagedResponse<LocationSummaryResponse>
                {
                    Items = locationResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommended locations for user: {UserId}", userId);
                return new PagedResponse<LocationSummaryResponse> { Items = new List<LocationSummaryResponse>() };
            }
        }

        #endregion

        #region Admin Reports

        public async Task<LocationAnalyticsReport> GenerateLocationReportAsync(Guid locationId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null)
                {
                    throw new KeyNotFoundException($"Location with ID {locationId} not found");
                }

                var report = new LocationAnalyticsReport
                {
                    LocationId = locationId,
                    LocationName = location.Name,
                    ReportStartDate = startDate,
                    ReportEndDate = endDate
                };

                // View stats
                report.ViewStats = await GetLocationViewStatsAsync(locationId, startDate, endDate);

                // Rating stats
                var reviews = await _reviewRepository.FindAsync(r =>
                    r.LocationId == locationId &&
                    r.CreatedAt >= startDate &&
                    r.CreatedAt <= endDate);

                var reviewList = reviews.ToList();
                report.NewReviews = reviewList.Count;

                if (reviewList.Any())
                {
                    report.RatingStats = new LocationRatingBreakdown
                    {
                        AverageOverall = reviewList.Average(r => (int)r.OverallRating),
                        TotalReviews = reviewList.Count,
                        RecommendedCount = reviewList.Count(r => r.IsRecommended),
                        RatingDistribution = reviewList
                            .GroupBy(r => (int)r.OverallRating)
                            .ToDictionary(g => g.Key, g => g.Count())
                    };
                }

                // Bookmark stats
                var bookmarks = await _bookmarkRepository.FindAsync(b =>
                    b.LocationId == locationId &&
                    b.CreatedAt >= startDate &&
                    b.CreatedAt <= endDate);
                report.NewBookmarks = bookmarks.Count();

                // View behavior analysis
                var views = await LocationViews.Find(v =>
                    v.LocationId == locationId &&
                    v.ViewedAt >= startDate &&
                    v.ViewedAt <= endDate).ToListAsync();

                if (views.Any())
                {
                    report.AverageSessionDuration = views.Average(v => v.SessionDuration);
                    report.ConversionRate = views.Any() ? (double)report.NewBookmarks / views.Count * 100 : 0;
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating location report: {LocationId}", locationId);
                throw;
            }
        }

        public async Task<GlobalLocationReport> GenerateGlobalLocationReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var report = new GlobalLocationReport
                {
                    ReportStartDate = startDate,
                    ReportEndDate = endDate
                };

                // Location counts
                var allLocations = await _locationRepository.FindAsync(l => !l.IsDeleted);
                var locationList = allLocations.ToList();

                report.TotalLocations = locationList.Count;
                report.ActiveLocations = locationList.Count(l => l.Status == LocationStatus.Active);
                report.PendingLocations = locationList.Count(l => l.Status == LocationStatus.Pending);
                report.SponsoredLocations = locationList.Count(l => l.IsSponsoredAndActive);

                // View stats
                var views = await LocationViews.Find(v =>
                    v.ViewedAt >= startDate &&
                    v.ViewedAt <= endDate).ToListAsync();
                report.TotalViews = views.Count;

                // Review stats
                var reviews = await _reviewRepository.FindAsync(r =>
                    r.CreatedAt >= startDate &&
                    r.CreatedAt <= endDate);
                var reviewList = reviews.ToList();
                report.TotalReviews = reviewList.Count;
                report.GlobalAverageRating = reviewList.Any() ? reviewList.Average(r => (int)r.OverallRating) : 0;

                // Distribution stats
                report.LocationsByType = locationList
                    .GroupBy(l => l.Type)
                    .ToDictionary(g => g.Key, g => g.Count());

                report.LocationsByCountry = locationList
                    .GroupBy(l => l.Country)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());

                // Top features
                var featureCounts = new Dictionary<string, int>();
                foreach (var location in locationList)
                {
                    foreach (LocationFeature feature in Enum.GetValues<LocationFeature>())
                    {
                        if (feature != LocationFeature.None && location.HasFeature(feature))
                        {
                            var featureName = feature.ToString();
                            featureCounts[featureName] = featureCounts.GetValueOrDefault(featureName, 0) + 1;
                        }
                    }
                }
                report.TopFeatures = featureCounts.OrderByDescending(kv => kv.Value).Take(10).ToDictionary(kv => kv.Key, kv => kv.Value);

                // Trending and top rated
                var trendingResult = await GetTrendingLocationsAsync(1, 10, endDate - startDate);
                report.TrendingLocations = trendingResult.Items.ToList();

                var topRatedResult = await GetHighestRatedLocationsAsync(1, 10);
                report.TopRatedLocations = topRatedResult.Items.ToList();

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating global location report");
                throw;
            }
        }

        #endregion

        #region Interaction Tracking

        public async Task RecordLocationInteractionAsync(Guid locationId, Guid userId, string interactionType, Dictionary<string, object> metadata = null)
        {
            try
            {
                var interaction = new LocationInteraction
                {
                    LocationId = locationId,
                    UserId = userId,
                    InteractionType = interactionType,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = metadata ?? new Dictionary<string, object>()
                };

                await LocationInteractions.InsertOneAsync(interaction);
                _logger.LogDebug("Location interaction recorded: {LocationId} - {InteractionType} by {UserId}", locationId, interactionType, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording location interaction");
            }
        }

        public async Task RecordSearchMetricsAsync(string searchTerm, List<string> filters, int resultCount, Guid? userId = null)
        {
            try
            {
                var searchMetrics = new LocationSearchMetrics
                {
                    SearchTerm = searchTerm,
                    Filters = filters ?? new List<string>(),
                    ResultCount = resultCount,
                    UserId = userId,
                    SearchedAt = DateTime.UtcNow
                };

                await SearchMetrics.InsertOneAsync(searchMetrics);
                _logger.LogDebug("Search metrics recorded: {SearchTerm} - {ResultCount} results", searchTerm, resultCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording search metrics");
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task UpdateDailyMetricsAsync(Guid locationId, DateTime date)
        {
            try
            {
                var filter = Builders<LocationPopularityMetrics>.Filter.And(
                    Builders<LocationPopularityMetrics>.Filter.Eq(m => m.LocationId, locationId),
                    Builders<LocationPopularityMetrics>.Filter.Eq(m => m.Date, date)
                );

                var existingMetrics = await PopularityMetrics.Find(filter).FirstOrDefaultAsync();

                if (existingMetrics == null)
                {
                    // Yeni günlük metrik oluştur
                    var newMetrics = new LocationPopularityMetrics
                    {
                        LocationId = locationId,
                        Date = date,
                        ViewCount = 1,
                        UniqueViewCount = 1,
                        LastUpdated = DateTime.UtcNow
                    };

                    await PopularityMetrics.InsertOneAsync(newMetrics);
                }
                else
                {
                    // Mevcut metriki güncelle
                    var update = Builders<LocationPopularityMetrics>.Update
                        .Inc(m => m.ViewCount, 1)
                        .Set(m => m.LastUpdated, DateTime.UtcNow);

                    await PopularityMetrics.UpdateOneAsync(filter, update);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating daily metrics for location: {LocationId}", locationId);
            }
        }

        private string GenerateSessionId(Guid? userId, string ipAddress)
        {
            var input = $"{userId}_{ipAddress}_{DateTime.UtcNow.Date}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Replace("=", "").Substring(0, 16);
        }

        private async Task<List<LocationSummaryResponse>> MapToLocationSummaryResponsesAsync(List<Location> locations)
        {
            var responses = new List<LocationSummaryResponse>();

            foreach (var location in locations)
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
                    Type = location.Type.ToString(),
                    Status = location.Status.ToString(),
                    IsSponsored = location.IsSponsoredAndActive,
                    SponsoredPriority = location.SponsoredPriority,
                    HasEntryFee = location.HasEntryFee,
                    EntryFee = location.EntryFee,
                    Currency = location.Currency,
                    AverageRating = location.AverageRating,
                    ReviewCount = location.ReviewCount,
                };

                responses.Add(response);
            }

            return responses;
        }

        #endregion

        #region Data Cleanup

        public async Task CleanupOldAnalyticsDataAsync(int retentionDays = 90)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                // Eski view recordlarını sil
                var viewFilter = Builders<LocationView>.Filter.Lt(v => v.ViewedAt, cutoffDate);
                var viewResult = await LocationViews.DeleteManyAsync(viewFilter);

                // Eski interaction recordlarını sil
                var interactionFilter = Builders<LocationInteraction>.Filter.Lt(i => i.CreatedAt, cutoffDate);
                var interactionResult = await LocationInteractions.DeleteManyAsync(interactionFilter);

                // Eski search metrics'leri sil
                var searchFilter = Builders<LocationSearchMetrics>.Filter.Lt(s => s.SearchedAt, cutoffDate);
                var searchResult = await SearchMetrics.DeleteManyAsync(searchFilter);

                _logger.LogInformation("Analytics data cleanup completed. Deleted: {ViewRecords} views, {InteractionRecords} interactions, {SearchRecords} searches",
                    viewResult.DeletedCount, interactionResult.DeletedCount, searchResult.DeletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during analytics data cleanup");
            }
        }

        #endregion

        #region Performance Optimization Methods

        public async Task OptimizeAnalyticsPerformanceAsync()
        {
            try
            {
                // Create indexes for better query performance
                await CreateAnalyticsIndexesAsync();

                // Aggregate old daily metrics into monthly metrics
                await AggregateOldMetricsAsync();

                _logger.LogInformation("Analytics performance optimization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during analytics performance optimization");
            }
        }

        private async Task CreateAnalyticsIndexesAsync()
        {
            try
            {
                // LocationViews indexes
                var locationViewIndexes = new[]
                {
                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys
                            .Ascending(v => v.LocationId)
                            .Ascending(v => v.ViewedAt)),
                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys
                            .Ascending(v => v.UserId)
                            .Ascending(v => v.ViewedAt)),
                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys.Ascending(v => v.SessionId)),
                    new CreateIndexModel<LocationView>(
                        Builders<LocationView>.IndexKeys.Ascending(v => v.ViewedAt))
                };

                await LocationViews.Indexes.CreateManyAsync(locationViewIndexes);

                // LocationInteractions indexes
                var interactionIndexes = new[]
                {
                    new CreateIndexModel<LocationInteraction>(
                        Builders<LocationInteraction>.IndexKeys
                            .Ascending(i => i.LocationId)
                            .Ascending(i => i.CreatedAt)),
                    new CreateIndexModel<LocationInteraction>(
                        Builders<LocationInteraction>.IndexKeys
                            .Ascending(i => i.UserId)
                            .Ascending(i => i.CreatedAt)),
                    new CreateIndexModel<LocationInteraction>(
                        Builders<LocationInteraction>.IndexKeys.Ascending(i => i.InteractionType))
                };

                await LocationInteractions.Indexes.CreateManyAsync(interactionIndexes);

                // PopularityMetrics indexes
                var metricsIndexes = new[]
                {
                    new CreateIndexModel<LocationPopularityMetrics>(
                        Builders<LocationPopularityMetrics>.IndexKeys
                            .Ascending(m => m.LocationId)
                            .Ascending(m => m.Date)),
                    new CreateIndexModel<LocationPopularityMetrics>(
                        Builders<LocationPopularityMetrics>.IndexKeys.Ascending(m => m.Date))
                };

                await PopularityMetrics.Indexes.CreateManyAsync(metricsIndexes);

                // SearchMetrics indexes
                var searchIndexes = new[]
                {
                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys.Ascending(s => s.SearchTerm)),
                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys.Ascending(s => s.SearchedAt)),
                    new CreateIndexModel<LocationSearchMetrics>(
                        Builders<LocationSearchMetrics>.IndexKeys
                            .Ascending(s => s.UserId)
                            .Ascending(s => s.SearchedAt))
                };

                await SearchMetrics.Indexes.CreateManyAsync(searchIndexes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Some indexes may already exist or failed to create");
            }
        }

        private async Task AggregateOldMetricsAsync()
        {
            try
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                // Aggregate daily metrics older than 30 days into monthly summaries
                var pipeline = new[]
                {
                    new BsonDocument("$match", new BsonDocument("date", new BsonDocument("$lt", thirtyDaysAgo))),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", new BsonDocument
                            {
                                { "locationId", "$locationId" },
                                { "year", new BsonDocument("$year", "$date") },
                                { "month", new BsonDocument("$month", "$date") }
                            }
                        },
                        { "totalViews", new BsonDocument("$sum", "$viewCount") },
                        { "totalUniqueViews", new BsonDocument("$sum", "$uniqueViewCount") },
                        { "totalBookmarks", new BsonDocument("$sum", "$bookmarkCount") },
                        { "totalShares", new BsonDocument("$sum", "$shareCount") },
                        { "avgRating", new BsonDocument("$avg", "$averageRating") },
                        { "totalReviews", new BsonDocument("$sum", "$reviewCount") }
                    })
                };

                var monthlyAggregates = await PopularityMetrics.Aggregate<BsonDocument>(pipeline).ToListAsync();

                _logger.LogInformation("Aggregated {Count} monthly metrics from daily data", monthlyAggregates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating old metrics");
            }
        }

        #endregion

        #region Advanced Analytics Methods

        public async Task<Dictionary<string, object>> GetAdvancedLocationAnalyticsAsync(Guid locationId, int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days);
                var analytics = new Dictionary<string, object>();

                // User engagement metrics
                var views = await LocationViews.Find(v =>
                    v.LocationId == locationId &&
                    v.ViewedAt >= startDate).ToListAsync();

                if (views.Any())
                {
                    analytics["AverageSessionDuration"] = views.Average(v => v.SessionDuration);
                    analytics["BounceRate"] = views.Count(v => v.IsBounce) / (double)views.Count * 100;
                    analytics["ReturnVisitorRate"] = views.GroupBy(v => v.UserId)
                        .Count(g => g.Count() > 1) / (double)views.GroupBy(v => v.UserId).Count() * 100;
                }

                // Device and platform breakdown
                analytics["DeviceBreakdown"] = views
                    .GroupBy(v => v.DeviceType)
                    .ToDictionary(g => g.Key, g => g.Count());

                analytics["PlatformBreakdown"] = views
                    .GroupBy(v => v.Platform)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Geographic breakdown
                analytics["CountryBreakdown"] = views
                    .Where(v => !string.IsNullOrEmpty(v.Country))
                    .GroupBy(v => v.Country)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Time-based patterns
                analytics["HourlyPattern"] = views
                    .GroupBy(v => v.ViewedAt.Hour)
                    .ToDictionary(g => g.Key, g => g.Count());

                analytics["DayOfWeekPattern"] = views
                    .GroupBy(v => v.ViewedAt.DayOfWeek.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                // Interaction rates
                var interactions = await LocationInteractions.Find(i =>
                    i.LocationId == locationId &&
                    i.CreatedAt >= startDate).ToListAsync();

                if (views.Any())
                {
                    analytics["InteractionRate"] = interactions.Count / (double)views.Count * 100;
                    analytics["InteractionBreakdown"] = interactions
                        .GroupBy(i => i.InteractionType)
                        .ToDictionary(g => g.Key, g => g.Count());
                }

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting advanced location analytics: {LocationId}", locationId);
                return new Dictionary<string, object>();
            }
        }

        public async Task<Dictionary<string, object>> GetSearchAnalyticsAsync(int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days);
                var searches = await SearchMetrics.Find(s => s.SearchedAt >= startDate).ToListAsync();

                var analytics = new Dictionary<string, object>
                {
                    ["TotalSearches"] = searches.Count,
                    ["UniqueUsers"] = searches.Where(s => s.UserId.HasValue).Select(s => s.UserId).Distinct().Count(),
                    ["AverageResultCount"] = searches.Any() ? searches.Average(s => s.ResultCount) : 0
                };

                // Top search terms
                analytics["TopSearchTerms"] = searches
                    .Where(s => !string.IsNullOrEmpty(s.SearchTerm))
                    .GroupBy(s => s.SearchTerm.ToLowerInvariant())
                    .OrderByDescending(g => g.Count())
                    .Take(20)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Popular filters
                var allFilters = searches.SelectMany(s => s.Filters).ToList();
                analytics["PopularFilters"] = allFilters
                    .GroupBy(f => f)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Search patterns by hour
                analytics["SearchPatternByHour"] = searches
                    .GroupBy(s => s.SearchedAt.Hour)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Zero result searches
                var zeroResultSearches = searches.Where(s => s.ResultCount == 0).ToList();
                analytics["ZeroResultRate"] = searches.Any() ? zeroResultSearches.Count / (double)searches.Count * 100 : 0;
                analytics["TopZeroResultTerms"] = zeroResultSearches
                    .Where(s => !string.IsNullOrEmpty(s.SearchTerm))
                    .GroupBy(s => s.SearchTerm.ToLowerInvariant())
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count());

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting search analytics");
                return new Dictionary<string, object>();
            }
        }

        #endregion
    }
}
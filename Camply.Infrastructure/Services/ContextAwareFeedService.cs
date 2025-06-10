using Camply.Application.Common.Models;
using Camply.Application.MachineLearning.Interfaces;
using Camply.Application.Posts.DTOs;
using Camply.Domain.Analytics;
using Camply.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Camply.Infrastructure.Services.MachineLearning
{
    public class ContextAwareFeedService : IContextAwareFeedService
    {
        private readonly IMLFeedAlgorithmService _feedService;
        private readonly ILogger<ContextAwareFeedService> _logger;

        public ContextAwareFeedService(
            IMLFeedAlgorithmService feedService,
            ILogger<ContextAwareFeedService> logger)
        {
            _feedService = feedService;
            _logger = logger;
        }

        public async Task<UserContext> BuildUserContextAsync(Guid userId, HttpContext httpContext)
        {
            try
            {
                var context = new UserContext
                {
                    Timestamp = DateTime.UtcNow,
                    DeviceType = GetDeviceType(httpContext),
                    SessionId = GetSessionId(httpContext)
                };

                if (TryGetLocationFromRequest(httpContext, out var lat, out var lon))
                {
                    context.Latitude = lat;
                    context.Longitude = lon;

                    context = await EnrichContextWithLocationDataAsync(context);
                }

                // Add session duration if available
                if (httpContext.Session.Keys.Contains("session_start"))
                {
                    var sessionStart = DateTime.Parse(httpContext.Session.GetString("session_start"));
                    context.SessionDuration = DateTime.UtcNow - sessionStart;
                }
                else
                {
                    httpContext.Session.SetString("session_start", DateTime.UtcNow.ToString());
                    context.SessionDuration = TimeSpan.Zero;
                }

                // Add additional context data
                context.AdditionalData = new Dictionary<string, object>
                {
                    ["user_agent"] = httpContext.Request.Headers["User-Agent"].ToString(),
                    ["referrer"] = httpContext.Request.Headers["Referer"].ToString(),
                    ["accept_language"] = httpContext.Request.Headers["Accept-Language"].ToString(),
                    ["is_mobile"] = IsMobileDevice(httpContext),
                    ["time_of_day"] = GetTimeOfDay(context.Timestamp),
                    ["day_of_week"] = context.Timestamp.DayOfWeek.ToString()
                };

                _logger.LogDebug("Built user context for user {UserId}: Device={DeviceType}, HasLocation={HasLocation}, HasWeather={HasWeather}",
                    userId, context.DeviceType, context.Latitude.HasValue, context.Weather != null);

                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building user context for user {UserId}", userId);

                // Return minimal context on error
                return new UserContext
                {
                    Timestamp = DateTime.UtcNow,
                    DeviceType = "unknown",
                    SessionId = Guid.NewGuid().ToString()
                };
            }
        }

        public async Task<List<PostSummaryResponseML>> GetContextualizedFeedAsync(Guid userId, UserContext context, int count)
        {
            try
            {
                _logger.LogInformation("Getting contextualized feed for user {UserId} with count {Count}", userId, count);

                // Get base personalized feed
                var feedResponse = await _feedService.GeneratePersonalizedFeedAsync(userId, 1, Math.Min(count * 2, 100)); // Get more to allow for filtering
                var posts = feedResponse.Items.ToList();

                if (!posts.Any())
                {
                    _logger.LogWarning("No posts found in base feed for user {UserId}", userId);
                    return new List<PostSummaryResponseML>();
                }

                // Apply contextual boosting to each post
                var contextualizedPosts = new List<(PostSummaryResponseML post, double contextScore)>();

                foreach (var post in posts)
                {
                    try
                    {
                        var contextualBoost = await CalculateContextualBoostAsync(post, context);
                        var finalScore = post.PersonalizationScore * contextualBoost;

                        contextualizedPosts.Add((post, finalScore));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error calculating contextual boost for post {PostId}", post.Id);
                        contextualizedPosts.Add((post, post.PersonalizationScore)); // Use original score as fallback
                    }
                }

                // Sort by contextualized score and take requested count
                var finalPosts = contextualizedPosts
                    .OrderByDescending(x => x.contextScore)
                    .Take(count)
                    .Select(x =>
                    {
                        x.post.PersonalizationScore = x.contextScore;
                        return x.post;
                    })
                    .ToList();

                _logger.LogInformation("Generated contextualized feed for user {UserId}: {Count} posts with avg score {AvgScore:F3}",
                    userId, finalPosts.Count, finalPosts.Average(p => p.PersonalizationScore));

                return finalPosts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contextualized feed for user {UserId}", userId);

                // Fallback to regular feed
                try
                {
                    var fallbackFeed = await _feedService.GeneratePersonalizedFeedAsync(userId, 1, count);
                    return fallbackFeed.Items.ToList();
                }
                catch
                {
                    return new List<PostSummaryResponseML>();
                }
            }
        }

        public async Task<UserContext> EnrichContextWithLocationDataAsync(UserContext context)
        {
            if (!context.Latitude.HasValue || !context.Longitude.HasValue)
                return context;

            try
            {
                // Here you could add reverse geocoding to get location names, timezone, etc.
                // For now, we'll add some basic location-based context

                context.AdditionalData ??= new Dictionary<string, object>();
                context.AdditionalData["location_type"] = DetermineLocationType(context.Latitude.Value, context.Longitude.Value);
                context.AdditionalData["timezone_offset"] = GetTimezoneOffset(context.Latitude.Value, context.Longitude.Value);

                _logger.LogDebug("Enriched context with location data for coordinates {Lat}, {Lon}",
                    context.Latitude, context.Longitude);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enriching context with location data");
            }

            return context;
        }

   
        public async Task<double> CalculateContextualBoostAsync(PostSummaryResponseML post, UserContext context)
        {
            try
            {
                double boost = 1.0;

                // Weather-based boosting
                if (context.Weather != null)
                {
                    boost *= CalculateWeatherBoost(post, context.Weather);
                }

                // Time-based boosting
                boost *= CalculateTimeBasedBoost(post, context.Timestamp);

                // Location-based boosting
                if (context.Latitude.HasValue && context.Longitude.HasValue && post.Location != null)
                {
                    boost *= CalculateLocationBoost(post, context.Latitude.Value, context.Longitude.Value);
                }

                // Device-specific boosting
                boost *= CalculateDeviceBoost(post, context.DeviceType);

                // Session-based boosting
                boost *= CalculateSessionBoost(post, context.SessionDuration);

                // Seasonal boosting
                boost *= CalculateSeasonalBoost(post, context.Timestamp);

                return Math.Max(0.1, Math.Min(3.0, boost)); // Clamp between 0.1 and 3.0
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating contextual boost for post {PostId}", post.Id);
                return 1.0; // Return neutral boost on error
            }
        }

        // Private helper methods
        private double CalculateWeatherBoost(PostSummaryResponseML post, WeatherData weather)
        {
            double boost = 1.0;

            // Boost camping content during good weather
            if (weather.IsCampingWeather && IsCampingRelated(post))
            {
                boost *= 1.0 + (weather.CampingScore * 0.5); // Up to 50% boost
            }

            // Boost indoor content during bad weather
            if (!weather.IsCampingWeather && IsIndoorContent(post))
            {
                boost *= 1.3;
            }

            // Temperature-based boosting
            if (weather.Temperature > 25 && IsHotWeatherContent(post))
            {
                boost *= 1.2;
            }
            else if (weather.Temperature < 10 && IsColdWeatherContent(post))
            {
                boost *= 1.2;
            }

            return boost;
        }

        private double CalculateTimeBasedBoost(PostSummaryResponseML post, DateTime timestamp)
        {
            double boost = 1.0;
            var hour = timestamp.Hour;

            // Morning boost for inspirational content (6-10 AM)
            if (hour >= 6 && hour <= 10 && IsInspirationalContent(post))
            {
                boost *= 1.2;
            }

            // Evening boost for planning content (6-10 PM)
            if (hour >= 18 && hour <= 22 && IsPlanningContent(post))
            {
                boost *= 1.25;
            }

            // Weekend boost for adventure content
            if ((timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday)
                && IsAdventureContent(post))
            {
                boost *= 1.15;
            }

            // Recency boost - more recent posts get slight boost
            var hoursSincePost = (timestamp - post.CreatedAt).TotalHours;
            if (hoursSincePost < 24)
            {
                boost *= 1.0 + (0.1 * Math.Exp(-hoursSincePost / 12)); // Exponential decay boost
            }

            return boost;
        }

        private double CalculateLocationBoost(PostSummaryResponseML post, double userLat, double userLon)
        {
            if (post.Location?.Latitude == null || post.Location?.Longitude == null)
                return 1.0;

            var distance = CalculateDistance(userLat, userLon,
                post.Location.Latitude.Value, post.Location.Longitude.Value);

            // Strong boost for very nearby content (< 10km)
            if (distance < 10) return 2.0;

            // Medium boost for nearby content (< 50km)
            if (distance < 50) return 1.5;

            // Small boost for regional content (< 200km)
            if (distance < 200) return 1.2;

            // No location boost for distant content
            return 1.0;
        }

        private double CalculateDeviceBoost(PostSummaryResponseML post, string deviceType)
        {
            double boost = 1.0;

            if (deviceType == "mobile")
            {
                // Boost shorter content on mobile
                if (post.Content?.Length < 200)
                {
                    boost *= 1.1;
                }

                // Boost visual content on mobile
                if (post.Media?.Any() == true)
                {
                    boost *= 1.15;
                }
            }
            else if (deviceType == "desktop")
            {
                // Boost longer, detailed content on desktop
                if (post.Content?.Length > 500)
                {
                    boost *= 1.1;
                }
            }

            return boost;
        }

        private double CalculateSessionBoost(PostSummaryResponseML post, TimeSpan sessionDuration)
        {
            double boost = 1.0;

            // Boost engaging content for longer sessions
            if (sessionDuration.TotalMinutes > 30)
            {
                if (post.LikeCount + post.CommentCount > 10)
                {
                    boost *= 1.1;
                }
            }

            // Boost quick-read content for short sessions
            if (sessionDuration.TotalMinutes < 5)
            {
                if (post.Content?.Length < 150)
                {
                    boost *= 1.15;
                }
            }

            return boost;
        }

        private double CalculateSeasonalBoost(PostSummaryResponseML post, DateTime timestamp)
        {
            double boost = 1.0;
            var month = timestamp.Month;

            // Spring (March-May): Nature awakening content
            if (month >= 3 && month <= 5 && IsNatureContent(post))
            {
                boost *= 1.2;
            }

            // Summer (June-August): Camping and outdoor activities
            if (month >= 6 && month <= 8 && IsCampingRelated(post))
            {
                boost *= 1.3;
            }

            // Fall (September-November): Hiking and scenic content
            if (month >= 9 && month <= 11 && IsHikingContent(post))
            {
                boost *= 1.25;
            }

            // Winter (December-February): Indoor activities and winter camping
            if ((month >= 12 || month <= 2) && IsWinterContent(post))
            {
                boost *= 1.2;
            }

            return boost;
        }

        // Content classification helper methods
        private bool IsCampingRelated(PostSummaryResponseML post)
        {
            var keywords = new[] { "kamp", "camping", "çadır", "tent", "outdoor", "nature", "doğa" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsIndoorContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "indoor", "içeride", "ev", "home", "cooking", "yemek", "recipe" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsHotWeatherContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "swimming", "yüzme", "beach", "plaj", "water", "su", "cool", "serinlik" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsColdWeatherContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "winter", "kış", "snow", "kar", "warm", "sıcak", "fire", "ateş" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsInspirationalContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "beautiful", "güzel", "amazing", "muhteşem", "inspiring", "ilham", "dream", "rüya" };
            return ContainsKeywords(post.Content, keywords) || post.LikeCount > 20;
        }

        private bool IsPlanningContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "plan", "planning", "route", "rota", "guide", "rehber", "tip", "öneri" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsAdventureContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "adventure", "macera", "hike", "yürüyüş", "explore", "keşif", "trek" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsNatureContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "nature", "doğa", "forest", "orman", "mountain", "dağ", "river", "nehir" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsHikingContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "hike", "hiking", "yürüyüş", "trail", "patika", "mountain", "dağ" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool IsWinterContent(PostSummaryResponseML post)
        {
            var keywords = new[] { "winter", "kış", "snow", "kar", "cold", "soğuk", "cozy", "sıcak" };
            return ContainsKeywords(post.Content, keywords);
        }

        private bool ContainsKeywords(string content, string[] keywords)
        {
            if (string.IsNullOrEmpty(content)) return false;
            var lowerContent = content.ToLower();
            return keywords.Any(keyword => lowerContent.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private string GetDeviceType(HttpContext context)
        {
            var userAgent = context.Request.Headers["User-Agent"].ToString().ToLower();
            if (userAgent.Contains("mobile")) return "mobile";
            if (userAgent.Contains("tablet")) return "tablet";
            return "desktop";
        }

        private string GetSessionId(HttpContext context)
        {
            return context.Session?.Id ??
                   context.Request.Headers["X-Session-Id"].FirstOrDefault() ??
                   Guid.NewGuid().ToString();
        }

        private bool TryGetLocationFromRequest(HttpContext context, out double lat, out double lon)
        {
            lat = lon = 0;

            // Try to get from headers (mobile app)
            if (context.Request.Headers.TryGetValue("X-Latitude", out var latHeader) &&
                context.Request.Headers.TryGetValue("X-Longitude", out var lonHeader))
            {
                return double.TryParse(latHeader, out lat) && double.TryParse(lonHeader, out lon);
            }

            // Try to get from query parameters (web app)
            if (context.Request.Query.TryGetValue("lat", out var latQuery) &&
                context.Request.Query.TryGetValue("lon", out var lonQuery))
            {
                return double.TryParse(latQuery, out lat) && double.TryParse(lonQuery, out lon);
            }

            return false;
        }

        private bool IsMobileDevice(HttpContext context)
        {
            var userAgent = context.Request.Headers["User-Agent"].ToString().ToLower();
            return userAgent.Contains("mobile") || userAgent.Contains("android") || userAgent.Contains("iphone");
        }

        private string GetTimeOfDay(DateTime timestamp)
        {
            var hour = timestamp.Hour;
            return hour switch
            {
                >= 5 and < 12 => "morning",
                >= 12 and < 17 => "afternoon",
                >= 17 and < 21 => "evening",
                _ => "night"
            };
        }

        private string DetermineLocationType(double latitude, double longitude)
        {
            // This would use a reverse geocoding service in production
            // For now, return a simplified classification
            return "outdoor_area";
        }

        private int GetTimezoneOffset(double latitude, double longitude)
        {
            // This would use a timezone service in production
            // For now, return a default offset based on longitude
            return (int)(longitude / 15);
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth's radius in kilometers
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
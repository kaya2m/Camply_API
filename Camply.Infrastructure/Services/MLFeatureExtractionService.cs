using Camply.Application.Common.Interfaces;
using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain;
using Camply.Domain.Analytics;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class MLFeatureExtractionService : IMLFeatureExtractionService
    {
        private readonly IRepository<Post> _postRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IMLAnalyticsRepository _analyticsRepository;
        private readonly IMLUserFeatureRepository _userFeatureRepository;
        private readonly IMLContentFeatureRepository _contentFeatureRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MLFeatureExtractionService> _logger;

        public MLFeatureExtractionService(
            IRepository<Post> postRepository,
            IRepository<User> userRepository,
            IMLAnalyticsRepository analyticsRepository,
            IMLUserFeatureRepository userFeatureRepository,
            IMLContentFeatureRepository contentFeatureRepository,
            ICacheService cacheService,
            ILogger<MLFeatureExtractionService> logger)
        {
            _postRepository = postRepository;
            _userRepository = userRepository;
            _analyticsRepository = analyticsRepository;
            _userFeatureRepository = userFeatureRepository;
            _contentFeatureRepository = contentFeatureRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<string> ExtractUserFeaturesAsync(Guid userId)
        {
            try
            {
                var cacheKey = $"user_features:{userId}";
                var cached = await _cacheService.GetAsync<string>(cacheKey);
                if (!string.IsNullOrEmpty(cached)) return cached;

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return "{}";

                var interactions = await _analyticsRepository.GetUserInteractionsAsync(
                    userId, DateTime.UtcNow.AddDays(-30));

                var interests = await _analyticsRepository.GetUserInterestsAsync(userId);

                var features = new UserFeatures
                {
                    AccountAge = (DateTime.UtcNow - user.CreatedAt).TotalDays,
                    FollowerCount = user.Followers?.Count ?? 0,
                    FollowingCount = user.Following?.Count ?? 0,
                    PostCount = user.Posts?.Count ?? 0,

                    AvgSessionLength = CalculateAverageSessionLength(interactions),
                    DailyActiveHours = CalculateActiveHours(interactions),
                    PreferredContentTypes = CalculateContentTypePreferences(interactions),

                    CampingInterest = interests?.Interests?.GetValueOrDefault("camping", 0.5) ?? 0.5,
                    NatureInterest = interests?.Interests?.GetValueOrDefault("nature", 0.5) ?? 0.5,
                    PhotographyInterest = interests?.Interests?.GetValueOrDefault("photography", 0.5) ?? 0.5,
                    TravelInterest = interests?.Interests?.GetValueOrDefault("travel", 0.5) ?? 0.5,

                    LikeToCommentRatio = CalculateLikeToCommentRatio(interactions),
                    ShareFrequency = CalculateShareFrequency(interactions),
                    BlogReadingFrequency = CalculateBlogReadingFrequency(interactions),

                    PreferredTimeOfDay = CalculatePreferredTimeOfDay(interactions),
                    WeekendActivity = CalculateWeekendActivity(interactions),

                    DaysSinceLastActive = CalculateDaysSinceLastActive(interactions),
                    RecentEngagementTrend = CalculateEngagementTrend(interactions)
                };

                var featuresJson = JsonSerializer.Serialize(features);

                await _cacheService.SetAsync(cacheKey, featuresJson, TimeSpan.FromHours(6));

                await _userFeatureRepository.UpdateUserFeatureAsync(
                    userId, "behavioral", featuresJson, CalculateFeatureQuality(features));

                return featuresJson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting user features for {UserId}", userId);
                return "{}";
            }
        }

        public async Task<string> ExtractContentFeaturesAsync(Guid contentId, string contentType)
        {
            try
            {
                var cacheKey = $"content_features:{contentType}:{contentId}";
                var cached = await _cacheService.GetAsync<string>(cacheKey);
                if (!string.IsNullOrEmpty(cached)) return cached;

                ContentFeatures features;

                if (contentType == "Post")
                {
                    var post = await _postRepository.GetByIdAsync(contentId);
                    if (post == null) return "{}";
                    features = ExtractPostFeatures(post);
                }
                else
                {
                    // Handle other content types (Blog, etc.)
                    return "{}";
                }

                var featuresJson = JsonSerializer.Serialize(features);

                await _cacheService.SetAsync(cacheKey, featuresJson, TimeSpan.FromHours(2));

                await _contentFeatureRepository.UpdateContentFeatureAsync(
                    contentId, contentType, "general", featuresJson, features.QualityScore);

                return featuresJson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content features for {ContentType}:{ContentId}", contentType, contentId);
                return "{}";
            }
        }

        public async Task<bool> UpdateUserInterestProfileAsync(Guid userId)
        {
            try
            {
                var interactions = await _analyticsRepository.GetUserInteractionsAsync(
                    userId, DateTime.UtcNow.AddDays(-30));

                var interestScores = new Dictionary<string, double>();

                foreach (var interaction in interactions)
                {
                    var contentFeatures = await ExtractContentFeaturesAsync(interaction.ContentId, interaction.ContentType);
                    var features = JsonSerializer.Deserialize<ContentFeatures>(contentFeatures);

                    if (features != null)
                    {
                        var weight = GetInteractionWeight(interaction.InteractionType);

                        foreach (var category in features.Categories)
                        {
                            if (!interestScores.ContainsKey(category))
                                interestScores[category] = 0;

                            interestScores[category] += weight;
                        }
                    }
                }

                var maxScore = interestScores.Values.DefaultIfEmpty(1).Max();
                var normalizedScores = interestScores.ToDictionary(
                    kvp => kvp.Key,
                    kvp => Math.Min(kvp.Value / maxScore, 1.0));

                var interests = new UserInterestDocument
                {
                    UserId = userId,
                    Interests = normalizedScores,
                    UpdatedAt = DateTime.UtcNow,
                    Version = "v1.0"
                };

                await _analyticsRepository.SaveUserInterestsAsync(interests);

                await _cacheService.RemoveAsync($"user_features:{userId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user interest profile for {UserId}", userId);
                return false;
            }
        }

        public async Task<Dictionary<string, double>> CalculateContentSimilarityAsync(Guid contentId1, Guid contentId2)
        {
            try
            {
                var features1Json = await ExtractContentFeaturesAsync(contentId1, "Post");
                var features2Json = await ExtractContentFeaturesAsync(contentId2, "Post");

                var features1 = JsonSerializer.Deserialize<ContentFeatures>(features1Json);
                var features2 = JsonSerializer.Deserialize<ContentFeatures>(features2Json);

                if (features1 == null || features2 == null)
                    return new Dictionary<string, double> { ["overall"] = 0.0 };

                var similarities = new Dictionary<string, double>();

                similarities["text"] = CalculateTextSimilarity(features1.TextFeatures, features2.TextFeatures);

                similarities["category"] = CalculateCategorySimilarity(features1.Categories, features2.Categories);

                similarities["tag"] = CalculateTagSimilarity(features1.Tags, features2.Tags);

                if (features1.HasLocation && features2.HasLocation)
                {
                    similarities["location"] = CalculateLocationSimilarity(
                        features1.LocationFeatures, features2.LocationFeatures);
                }

                similarities["overall"] =
                    similarities["text"] * 0.4 +
                    similarities["category"] * 0.3 +
                    similarities["tag"] * 0.2 +
                    similarities.GetValueOrDefault("location", 0) * 0.1;

                return similarities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating content similarity between {ContentId1} and {ContentId2}", contentId1, contentId2);
                return new Dictionary<string, double> { ["overall"] = 0.0 };
            }
        }

        private ContentFeatures ExtractPostFeatures(Post post)
        {
            var features = new ContentFeatures
            {
                ContentLength = post.Content?.Length ?? 0,
                WordCount = CountWords(post.Content ?? ""),
                HasMedia = post.Media?.Any() ?? false,
                MediaCount = post.Media?.Count ?? 0,
                HasLocation = post.LocationId.HasValue,
                CreatedAt = post.CreatedAt,
                AuthorFollowerCount = post.User?.Followers?.Count ?? 0,

                LikeCount = post.Likes?.Count ?? 0,
                CommentCount = post.Comments?.Count ?? 0,

                TextFeatures = ExtractTextFeatures(post.Content ?? ""),
                Categories = ExtractCategories(post),
                Tags = post.Tags?.Select(pt => pt.Tag?.Name ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList() ?? new List<string>(),

                QualityScore = CalculateContentQuality(post)
            };

            if (features.HasLocation && post.Location != null)
            {
                features.LocationFeatures = new LocationFeatures
                {
                    Latitude = post.Location.Latitude,
                    Longitude = post.Location.Longitude,
                    Type = post.Location.Type
                };
            }

            return features;
        }

        private TextFeatures ExtractTextFeatures(string content)
        {
            var features = new TextFeatures();

            features.Length = content.Length;
            features.WordCount = CountWords(content);
            features.SentenceCount = CountSentences(content);

            features.SentimentScore = CalculateSimpleSentiment(content);

            features.Keywords = ExtractKeywords(content);

            features.HasQuestion = content.Contains("?");
            features.HasHashtags = content.Contains("#");
            features.HasMentions = content.Contains("@");
            features.HasUrls = Regex.IsMatch(content, @"http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+");

            return features;
        }

        private List<string> ExtractCategories(Post post)
        {
            var categories = new List<string>();
            var content = post.Content?.ToLower() ?? "";

            if (ContainsKeywords(content, new[] { "kamp", "camping", "çadır", "tent", "doğa", "nature" }))
                categories.Add("camping");

            if (ContainsKeywords(content, new[] { "fotoğraf", "photo", "resim", "image", "çekim" }))
                categories.Add("photography");

            if (ContainsKeywords(content, new[] { "seyahat", "travel", "gezi", "trip", "tatil", "vacation" }))
                categories.Add("travel");

            if (ContainsKeywords(content, new[] { "yemek", "food", "yemek", "cooking", "tarif", "recipe" }))
                categories.Add("food");

            if (post.LocationId.HasValue)
                categories.Add("location_based");

            if (post.Media?.Any() == true)
                categories.Add("media_rich");

            if (categories.Count == 0)
                categories.Add("general");

            return categories;
        }

        private bool ContainsKeywords(string content, string[] keywords)
        {
            return keywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private int CountSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private double CalculateSimpleSentiment(string content)
        {
            // Simplified sentiment analysis
            var positiveWords = new[] { "harika", "güzel", "muhteşem", "amazing", "beautiful", "love", "great", "awesome", "perfect" };
            var negativeWords = new[] { "kötü", "berbat", "terrible", "bad", "awful", "hate", "horrible", "worst" };

            var lowerContent = content.ToLower();
            var positiveCount = positiveWords.Count(word => lowerContent.Contains(word));
            var negativeCount = negativeWords.Count(word => lowerContent.Contains(word));

            if (positiveCount + negativeCount == 0) return 0.5; // Neutral

            return (double)positiveCount / (positiveCount + negativeCount);
        }

        private List<string> ExtractKeywords(string content)
        {
            // Simple keyword extraction
            var words = content.ToLower()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            return words;
        }

        private double CalculateContentQuality(Post post)
        {
            var score = 0.0;

            // Content length factor
            var contentLength = post.Content?.Length ?? 0;
            if (contentLength > 50) score += 0.2;
            if (contentLength > 200) score += 0.2;

            // Media factor
            if (post.Media?.Any() == true) score += 0.3;

            // Location factor
            if (post.LocationId.HasValue) score += 0.1;

            // Tags factor
            if (post.Tags?.Any() == true) score += 0.1;

            // Author credibility (follower count)
            var followerCount = post.User?.Followers?.Count ?? 0;
            if (followerCount > 10) score += 0.1;
            if (followerCount > 100) score += 0.1;

            return Math.Min(score, 1.0);
        }

        private double CalculateAverageSessionLength(List<UserInteractionDocument> interactions)
        {
            if (!interactions.Any()) return 0;

            var sessionDurations = interactions
                .Where(i => i.ViewDuration > 0)
                .Select(i => (double)i.ViewDuration.Value)
                .ToList();

            return sessionDurations.Any() ? sessionDurations.Average() : 0;
        }

        private List<int> CalculateActiveHours(List<UserInteractionDocument> interactions)
        {
            return interactions
                .GroupBy(i => i.CreatedAt.Hour)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => g.Key)
                .ToList();
        }

        private Dictionary<string, double> CalculateContentTypePreferences(List<UserInteractionDocument> interactions)
        {
            var preferences = interactions
                .GroupBy(i => i.ContentType)
                .ToDictionary(g => g.Key, g => (double)g.Count());

            var total = preferences.Values.Sum();
            if (total > 0)
            {
                preferences = preferences.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / total);
            }

            return preferences;
        }

        private double CalculateLikeToCommentRatio(List<UserInteractionDocument> interactions)
        {
            var likes = interactions.Count(i => i.InteractionType == "like");
            var comments = interactions.Count(i => i.InteractionType == "comment");

            return comments > 0 ? (double)likes / comments : likes > 0 ? 10.0 : 1.0;
        }

        private double CalculateShareFrequency(List<UserInteractionDocument> interactions)
        {
            var shares = interactions.Count(i => i.InteractionType == "share");
            var totalInteractions = interactions.Count;

            return totalInteractions > 0 ? (double)shares / totalInteractions : 0;
        }

        private double CalculateBlogReadingFrequency(List<UserInteractionDocument> interactions)
        {
            var blogInteractions = interactions.Count(i => i.ContentType == "Blog");
            var totalInteractions = interactions.Count;

            return totalInteractions > 0 ? (double)blogInteractions / totalInteractions : 0;
        }

        private int CalculatePreferredTimeOfDay(List<UserInteractionDocument> interactions)
        {
            if (!interactions.Any()) return 12; // Default to noon

            return interactions
                .GroupBy(i => i.CreatedAt.Hour)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
        }

        private double CalculateWeekendActivity(List<UserInteractionDocument> interactions)
        {
            if (!interactions.Any()) return 0.5;

            var weekendInteractions = interactions.Count(i =>
                i.CreatedAt.DayOfWeek == DayOfWeek.Saturday ||
                i.CreatedAt.DayOfWeek == DayOfWeek.Sunday);

            return (double)weekendInteractions / interactions.Count;
        }

        private double CalculateDaysSinceLastActive(List<UserInteractionDocument> interactions)
        {
            if (!interactions.Any()) return 30; // Max value

            var lastInteraction = interactions.Max(i => i.CreatedAt);
            return (DateTime.UtcNow - lastInteraction).TotalDays;
        }

        private double CalculateEngagementTrend(List<UserInteractionDocument> interactions)
        {
            if (interactions.Count < 10) return 0.5; // Not enough data

            var recentInteractions = interactions
                .Where(i => i.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .Count();

            var previousInteractions = interactions
                .Where(i => i.CreatedAt >= DateTime.UtcNow.AddDays(-14) && i.CreatedAt < DateTime.UtcNow.AddDays(-7))
                .Count();

            if (previousInteractions == 0) return recentInteractions > 0 ? 1.0 : 0.5;

            return Math.Min((double)recentInteractions / previousInteractions, 2.0) / 2.0;
        }

        private float CalculateFeatureQuality(UserFeatures features)
        {
            var quality = 0.0f;

            // Account age factor
            if (features.AccountAge > 7) quality += 0.2f;
            if (features.AccountAge > 30) quality += 0.2f;

            // Activity factor
            if (features.PostCount > 5) quality += 0.2f;
            if (features.FollowerCount > 10) quality += 0.2f;

            // Engagement factor
            if (features.DaysSinceLastActive < 7) quality += 0.2f;

            return Math.Min(quality, 1.0f);
        }

        private double GetInteractionWeight(string interactionType)
        {
            return interactionType switch
            {
                "view" => 0.1,
                "like" => 0.3,
                "comment" => 0.5,
                "share" => 0.7,
                "save" => 0.6,
                "follow" => 0.8,
                _ => 0.1
            };
        }

        private double CalculateTextSimilarity(TextFeatures features1, TextFeatures features2)
        {
            if (features1?.Keywords == null || features2?.Keywords == null) return 0;

            var keywords1 = new HashSet<string>(features1.Keywords);
            var keywords2 = new HashSet<string>(features2.Keywords);

            var intersection = keywords1.Intersect(keywords2).Count();
            var union = keywords1.Union(keywords2).Count();

            return union > 0 ? (double)intersection / union : 0;
        }

        private double CalculateCategorySimilarity(List<string> categories1, List<string> categories2)
        {
            if (categories1?.Any() != true || categories2?.Any() != true) return 0;

            var set1 = new HashSet<string>(categories1);
            var set2 = new HashSet<string>(categories2);

            var intersection = set1.Intersect(set2).Count();
            var union = set1.Union(set2).Count();

            return union > 0 ? (double)intersection / union : 0;
        }

        private double CalculateTagSimilarity(List<string> tags1, List<string> tags2)
        {
            if (tags1?.Any() != true || tags2?.Any() != true) return 0;

            var set1 = new HashSet<string>(tags1);
            var set2 = new HashSet<string>(tags2);

            var intersection = set1.Intersect(set2).Count();
            var union = set1.Union(set2).Count();

            return union > 0 ? (double)intersection / union : 0;
        }

        private double CalculateLocationSimilarity(LocationFeatures loc1, LocationFeatures loc2)
        {
            if (loc1 == null || loc2 == null) return 0;

            // Calculate distance using Haversine formula (simplified)
            var lat1Rad = loc1.Latitude * Math.PI / 180;
            var lat2Rad = loc2.Latitude * Math.PI / 180;
            var deltaLat = (loc2.Latitude - loc1.Latitude) * Math.PI / 180;
            var deltaLon = (loc2.Longitude - loc1.Longitude) * Math.PI / 180;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = 6371 * c; // Earth's radius in kilometers

            // Convert distance to similarity (closer = more similar)
            return Math.Max(0, 1 - (distance / 1000)); // Normalize by 1000km
        }
    }

    // Feature Models
    public class UserFeatures
    {
        public double AccountAge { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public int PostCount { get; set; }
        public double AvgSessionLength { get; set; }
        public List<int> DailyActiveHours { get; set; } = new();
        public Dictionary<string, double> PreferredContentTypes { get; set; } = new();
        public double CampingInterest { get; set; }
        public double NatureInterest { get; set; }
        public double PhotographyInterest { get; set; }
        public double TravelInterest { get; set; }
        public double LikeToCommentRatio { get; set; }
        public double ShareFrequency { get; set; }
        public double BlogReadingFrequency { get; set; }
        public int PreferredTimeOfDay { get; set; }
        public double WeekendActivity { get; set; }
        public double DaysSinceLastActive { get; set; }
        public double RecentEngagementTrend { get; set; }
    }

    public class ContentFeatures
    {
        public int ContentLength { get; set; }
        public int WordCount { get; set; }
        public bool HasMedia { get; set; }
        public int MediaCount { get; set; }
        public bool HasLocation { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AuthorFollowerCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public TextFeatures TextFeatures { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public LocationFeatures LocationFeatures { get; set; }
        public double QualityScore { get; set; }
    }

    public class TextFeatures
    {
        public int Length { get; set; }
        public int WordCount { get; set; }
        public int SentenceCount { get; set; }
        public double SentimentScore { get; set; }
        public List<string> Keywords { get; set; } = new();
        public bool HasQuestion { get; set; }
        public bool HasHashtags { get; set; }
        public bool HasMentions { get; set; }
        public bool HasUrls { get; set; }
    }

    public class LocationFeatures
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public LocationType Type { get; set; }
    }
}

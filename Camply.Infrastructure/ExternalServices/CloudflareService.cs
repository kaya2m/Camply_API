using Camply.Application.Media.DTOs;
using Camply.Application.Media.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Camply.Infrastructure.ExternalServices
{
    public class CloudflareService : ICloudflareService
    {
        private readonly HttpClient _httpClient;
        private readonly CloudflareSettings _settings;
        private readonly ILogger<CloudflareService> _logger;

        public CloudflareService(
            HttpClient httpClient,
            IOptions<CloudflareSettings> settings,
            ILogger<CloudflareService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            // Setup HTTP client headers
            if (!string.IsNullOrEmpty(_settings.ApiToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
            }
            else if (!string.IsNullOrEmpty(_settings.ApiKey) && !string.IsNullOrEmpty(_settings.Email))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Auth-Email", _settings.Email);
                _httpClient.DefaultRequestHeaders.Add("X-Auth-Key", _settings.ApiKey);
            }
            else
            {
                _logger.LogWarning("Cloudflare API credentials are not properly configured");
            }
        }

        public async Task<string> GetOptimizedImageUrl(string originalUrl, int? width = null, int? height = null, string format = null)
        {
            try
            {
                if (string.IsNullOrEmpty(originalUrl))
                    return originalUrl;

                // Image Resizing kullanmadan sadece CDN'e yönlendir
                return ConvertToCdnUrl(originalUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating optimized image URL for {OriginalUrl}", originalUrl);
                return originalUrl;
            }
        }

        public async Task<bool> PurgeCache(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ZoneId))
                {
                    _logger.LogWarning("Zone ID is not configured, cannot purge cache");
                    return false;
                }

                var cdnUrl = ConvertToCdnUrl(url);
                var request = new
                {
                    files = new[] { cdnUrl }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://api.cloudflare.com/client/v4/zones/{_settings.ZoneId}/purge_cache",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully purged cache for {Url}", cdnUrl);
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to purge cache for {Url}: {StatusCode} - {Error}",
                    cdnUrl, response.StatusCode, error);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging cache for {Url}", url);
                return false;
            }
        }

        public async Task<bool> PurgeCacheByTags(string[] tags)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ZoneId))
                {
                    _logger.LogWarning("Zone ID is not configured, cannot purge cache by tags");
                    return false;
                }

                if (tags == null || tags.Length == 0)
                {
                    _logger.LogWarning("No tags provided for cache purge");
                    return false;
                }

                var request = new
                {
                    tags = tags
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://api.cloudflare.com/client/v4/zones/{_settings.ZoneId}/purge_cache",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully purged cache for tags: {Tags}", string.Join(", ", tags));
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to purge cache for tags {Tags}: {StatusCode} - {Error}",
                    string.Join(", ", tags), response.StatusCode, error);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging cache for tags: {Tags}", string.Join(", ", tags));
                return false;
            }
        }

        public async Task<CloudflareAnalytics> GetAnalytics(DateTime startDate, DateTime endDate)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.ZoneId))
                {
                    _logger.LogWarning("Zone ID is not configured, cannot get analytics");
                    return new CloudflareAnalytics();
                }

                var start = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var end = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

                var response = await _httpClient.GetAsync(
                    $"https://api.cloudflare.com/client/v4/zones/{_settings.ZoneId}/analytics/dashboard?since={start}&until={end}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var analytics = JsonSerializer.Deserialize<CloudflareAnalyticsResponse>(json, options);

                    _logger.LogDebug("Successfully retrieved analytics for period {Start} to {End}", start, end);
                    return analytics?.Result ?? new CloudflareAnalytics();
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to get analytics: {StatusCode} - {Error}", response.StatusCode, error);
                return new CloudflareAnalytics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Cloudflare analytics for period {StartDate} to {EndDate}", startDate, endDate);
                return new CloudflareAnalytics();
            }
        }

        #region Helper Methods

        private string ConvertToCdnUrl(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return originalUrl;

            try
            {
                // Convert Azure Blob URL to Cloudflare CDN URL
                // From: https://camplymedia.blob.core.windows.net/camply-media/...
                // To: https://media.thecamply.com/...

                if (originalUrl.Contains("blob.core.windows.net"))
                {
                    var uri = new Uri(originalUrl);
                    var pathSegments = uri.AbsolutePath.Split('/');

                    // Remove container name from path (first segment after domain)
                    if (pathSegments.Length > 2)
                    {
                        var pathWithoutContainer = string.Join("/", pathSegments.Skip(2));
                        var cdnUrl = $"https://{_settings.CdnDomain}/{pathWithoutContainer.TrimStart('/')}";

                        _logger.LogDebug("Converted {OriginalUrl} to CDN URL {CdnUrl}", originalUrl, cdnUrl);
                        return cdnUrl;
                    }
                }

                // If it's already a CDN URL or unknown format, return as is
                return originalUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting URL to CDN format: {OriginalUrl}", originalUrl);
                return originalUrl;
            }
        }

        private string ExtractPathFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath.TrimStart('/');

                _logger.LogDebug("Extracted path {Path} from URL {Url}", path, url);
                return path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting path from URL: {Url}", url);
                return string.Empty;
            }
        }

        #endregion
    }
}
using Camply.Application.Media.DTOs;
using Camply.Application.Media.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

            // Setup HTTP client
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Email", _settings.Email);
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Key", _settings.ApiKey);
        }

        public async Task<string> GetOptimizedImageUrl(string originalUrl, int? width = null, int? height = null, string format = null)
        {
            try
            {
                if (string.IsNullOrEmpty(originalUrl))
                    return originalUrl;

                // Convert Azure Blob URL to Cloudflare CDN URL
                var cdnUrl = ConvertToCdnUrl(originalUrl);

                if (!_settings.EnableImageOptimization)
                    return cdnUrl;

                // Build optimization parameters
                var parameters = new List<string>();

                if (width.HasValue)
                    parameters.Add($"w={width.Value}");

                if (height.HasValue)
                    parameters.Add($"h={height.Value}");

                if (!string.IsNullOrEmpty(format))
                    parameters.Add($"f={format}");
                else if (_settings.ImageOptions.AutoWebP)
                    parameters.Add("f=auto");

                if (_settings.ImageOptions.Quality < 100)
                    parameters.Add($"q={_settings.ImageOptions.Quality}");

                // Add fit parameter for better image handling
                if (width.HasValue && height.HasValue)
                    parameters.Add("fit=crop");

                if (parameters.Count > 0)
                {
                    var paramString = string.Join(",", parameters);
                    // Cloudflare Image Resizing format: /cdn-cgi/image/{options}/{url}
                    return $"https://{_settings.CdnDomain}/cdn-cgi/image/{paramString}/{ExtractPathFromUrl(cdnUrl)}";
                }

                return cdnUrl;
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
                var cdnUrl = ConvertToCdnUrl(url);
                var request = new
                {
                    files = new[] { cdnUrl }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://api.cloudflare.com/client/v4/zones/{_settings.ZoneId}/purge_cache",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully purged cache for {Url}", cdnUrl);
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to purge cache for {Url}: {Error}", cdnUrl, error);
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
                var request = new
                {
                    tags = tags
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://api.cloudflare.com/client/v4/zones/{_settings.ZoneId}/purge_cache",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully purged cache for tags: {Tags}", string.Join(", ", tags));
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to purge cache for tags {Tags}: {Error}", string.Join(", ", tags), error);
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
                var start = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var end = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

                var response = await _httpClient.GetAsync(
                    $"https://api.cloudflare.com/client/v4/zones/{_settings.ZoneId}/analytics/dashboard?since={start}&until={end}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var analytics = JsonSerializer.Deserialize<CloudflareAnalyticsResponse>(json);
                    return analytics?.Result ?? new CloudflareAnalytics();
                }

                return new CloudflareAnalytics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Cloudflare analytics");
                return new CloudflareAnalytics();
            }
        }

        #region Helper Methods

        private string ConvertToCdnUrl(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return originalUrl;

            // Convert Azure Blob URL to Cloudflare CDN URL
            // From: https://camplymedia.blob.core.windows.net/camply-media/...
            // To: https://media.thecamply.com/...

            if (originalUrl.Contains("blob.core.windows.net"))
            {
                var uri = new Uri(originalUrl);
                var pathSegments = uri.AbsolutePath.Split('/');

                // Remove container name from path
                if (pathSegments.Length > 2)
                {
                    var pathWithoutContainer = string.Join("/", pathSegments.Skip(2));
                    return $"https://{_settings.CdnDomain}/{pathWithoutContainer}";
                }
            }

            return originalUrl;
        }

        private string ExtractPathFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            var uri = new Uri(url);
            return uri.AbsolutePath.TrimStart('/');
        }

        #endregion
    }
}

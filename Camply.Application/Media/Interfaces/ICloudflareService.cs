using Camply.Application.Media.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Media.Interfaces
{
    public interface ICloudflareService
    {
        Task<string> GetOptimizedImageUrl(string originalUrl, int? width = null, int? height = null, string format = null);
        Task<bool> PurgeCache(string url);
        Task<bool> PurgeCacheByTags(string[] tags);
        Task<CloudflareAnalytics> GetAnalytics(DateTime startDate, DateTime endDate);
    }
}

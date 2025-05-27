using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class CloudflareSettings
    {
        public string ZoneId { get; set; }
        public string ApiToken { get; set; }
        public string ApiKey { get; set; }
        public string Email { get; set; }
        public string CdnDomain { get; set; } = "media.thecamply.com";
        public bool EnableImageOptimization { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int CacheTtl { get; set; } = 86400; // 24 hours

        // Image optimization settings
        public CloudflareImageOptions ImageOptions { get; set; } = new();
    }

    public class CloudflareImageOptions
    {
        public int Quality { get; set; } = 85;
        public bool AutoWebP { get; set; } = true;
        public bool AutoAVIF { get; set; } = true;
        public string[] AllowedFormats { get; set; } = { "webp", "jpeg", "png" };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Media.DTOs
{
    public class CloudflareAnalytics
    {
        public long Requests { get; set; }
        public long Bandwidth { get; set; }
        public double CacheRatio { get; set; }
        public Dictionary<string, long> StatusCodes { get; set; } = new();
    }

}

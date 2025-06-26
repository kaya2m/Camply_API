using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Locations.DTOs
{
    public class LocationViewStats
    {
        public Guid LocationId { get; set; }
        public int TotalViews { get; set; }
        public int UniqueViews { get; set; }
        public int ViewsToday { get; set; }
        public int ViewsThisWeek { get; set; }
        public int ViewsThisMonth { get; set; }
        public DateTime LastViewDate { get; set; }
        public Dictionary<string, int> DailyViews { get; set; } = new();
        public Dictionary<string, int> HourlyViews { get; set; } = new();
    }
}

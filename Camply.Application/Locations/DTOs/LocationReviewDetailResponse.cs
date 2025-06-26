using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Locations.DTOs
{
    public class LocationReviewDetailResponse : LocationReviewSummaryResponse
    {
        public string LocationName { get; set; }
        public Guid LocationId { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanRespond { get; set; }
        public List<ReviewHelpfulUser> HelpfulUsers { get; set; } = new();
        public ReviewMetrics Metrics { get; set; }
    }
}

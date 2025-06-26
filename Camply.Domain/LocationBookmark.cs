using Camply.Domain.Auth;
using Camply.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain
{
    public class LocationBookmark : BaseTrackingEntity
    {
        public Guid UserId { get; set; }
        public Guid LocationId { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Location Location { get; set; }
    }
}

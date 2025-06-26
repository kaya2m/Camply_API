using Camply.Domain.Auth;
using Camply.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain
{
    public class ReviewHelpful : BaseTrackingEntity
    {
        public Guid ReviewId { get; set; }
        public Guid UserId { get; set; }
        public bool IsHelpful { get; set; }

        // Navigation properties
        public virtual LocationReview Review { get; set; }
        public virtual User User { get; set; }
    }
}

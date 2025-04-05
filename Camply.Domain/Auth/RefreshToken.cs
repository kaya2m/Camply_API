using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Domain.Common;

namespace Camply.Domain.Auth
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public string ReplacedByToken { get; set; }
        public string CreatedByIp { get; set; }
        public string RevokedByIp { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string ReasonRevoked { get; set; }

        // Navigation properties
        public virtual User User { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiryDate;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}

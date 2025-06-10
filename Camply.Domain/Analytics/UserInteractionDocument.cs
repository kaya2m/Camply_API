using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Kullanıcı etkileşim verileri - MongoDB
    /// </summary>
    public class UserInteractionDocument
    {
        public ObjectId Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ContentId { get; set; }
        public string ContentType { get; set; } // "Post", "Blog"
        public string InteractionType { get; set; } // "view", "like", "comment", "share", "save"
        public DateTime CreatedAt { get; set; }
        public int? ViewDuration { get; set; } // Saniye cinsinden
        public float? ScrollDepth { get; set; } // 0-1 arası
        public string DeviceType { get; set; } // "mobile", "desktop", "tablet"
        public string SessionId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

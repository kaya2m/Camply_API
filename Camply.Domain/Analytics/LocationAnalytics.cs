using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Analytics
{
    public class LocationView
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid LocationId { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid? UserId { get; set; }

        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Referrer { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        public string Country { get; set; }
        public string City { get; set; }
        public string DeviceType { get; set; } // Mobile, Desktop, Tablet
        public string Platform { get; set; } // iOS, Android, Windows, etc.

        // Session bilgileri
        public string SessionId { get; set; }
        public int SessionDuration { get; set; } // Saniye cinsinden
        public bool IsUniqueView { get; set; } // Günlük unique view
        public bool IsBounce { get; set; } // Hemen çıkış
    }

    public class LocationInteraction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid LocationId { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }

        public string InteractionType { get; set; } // Bookmark, Share, Review, etc.

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class LocationPopularityMetrics
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid LocationId { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Date { get; set; }

        // Daily metrics
        public int ViewCount { get; set; }
        public int UniqueViewCount { get; set; }
        public int BookmarkCount { get; set; }
        public int ShareCount { get; set; }
        public int ReviewCount { get; set; }
        public double AverageRating { get; set; }
        public int SearchAppearances { get; set; }
        public int SearchClicks { get; set; }
        public double ClickThroughRate { get; set; }

        // Behavioral metrics
        public double AverageSessionDuration { get; set; }
        public double BounceRate { get; set; }
        public int ConversionCount { get; set; } // Bookmark veya contact action
        public double ConversionRate { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class LocationSearchMetrics
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string SearchTerm { get; set; }
        public List<string> Filters { get; set; } = new();
        public int ResultCount { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid? UserId { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;

        public string Location { get; set; } // Search location context
        public List<Guid> ClickedLocationIds { get; set; } = new();
        public int Position { get; set; } // Position in search results when clicked
    }
}

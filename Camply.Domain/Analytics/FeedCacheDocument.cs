using MongoDB.Bson;

namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Feed cache - MongoDB
    /// </summary>
    public class FeedCacheDocument
    {
        public ObjectId Id { get; set; }
        public Guid UserId { get; set; }
        public string FeedType { get; set; } // "home", "explore", "nearby"
        public List<CachedPost> Posts { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Page { get; set; }
        public string AlgorithmVersion { get; set; }
        public UserContext Context { get; set; } // İsteğin context'i
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

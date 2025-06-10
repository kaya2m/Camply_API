using MongoDB.Bson;

namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Feed impression tracking - MongoDB
    /// </summary>
    public class FeedImpressionDocument
    {
        public ObjectId Id { get; set; }
        public Guid UserId { get; set; }
        public Guid PostId { get; set; }
        public int Position { get; set; } // Feed'deki pozisyonu
        public string AlgorithmVersion { get; set; }
        public float PredictedScore { get; set; }
        public string Source { get; set; } // "following", "recommended", etc.
        public DateTime CreatedAt { get; set; }
        public bool WasClicked { get; set; }
        public bool WasLiked { get; set; }
        public bool WasShared { get; set; }
        public int? ViewDuration { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

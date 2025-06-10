using MongoDB.Bson;

namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Model tahmin sonuçları - MongoDB
    /// </summary>
    public class ModelPredictionDocument
    {
        public ObjectId Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ContentId { get; set; }
        public string ModelName { get; set; }
        public string ModelVersion { get; set; }
        public float PredictionScore { get; set; }
        public Dictionary<string, float> FeatureContributions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string PredictionType { get; set; } // "engagement", "click", "share", etc.
        public Dictionary<string, object> InputFeatures { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

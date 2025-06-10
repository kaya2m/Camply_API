using MongoDB.Bson;

namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Algoritma performans metrikleri - MongoDB
    /// </summary>
    public class AlgorithmMetricsDocument
    {
        public ObjectId Id { get; set; }
        public string MetricType { get; set; } // "ctr", "engagement", "retention", etc.
        public string AlgorithmVersion { get; set; }
        public DateTime Timestamp { get; set; }
        public float Value { get; set; }
        public Dictionary<string, float> SegmentedValues { get; set; } = new(); // Kullanıcı segmentlerine göre
        public string Granularity { get; set; } // "hourly", "daily", "weekly"
        public Dictionary<string, object> Dimensions { get; set; } = new(); // Ek boyutlar
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

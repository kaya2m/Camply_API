namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Cache'lenmiş post verisi
    /// </summary>
    public class CachedPost
    {
        public Guid PostId { get; set; }
        public float Score { get; set; }
        public string Source { get; set; } // "following", "interests", "trending", "nearby"
        public Dictionary<string, float> FeatureScores { get; set; } = new();
        public DateTime ScoredAt { get; set; }
    }
}

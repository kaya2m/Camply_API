namespace Camply.Domain.Analytics
{
    /// <summary>
    /// İlgi trend verisi
    /// </summary>
    public class InterestTrend
    {
        public DateTime Date { get; set; }
        public Dictionary<string, float> Interests { get; set; } = new();
        public float EngagementScore { get; set; }
    }
}

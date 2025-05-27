public class MediaAnalyticsResponse
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalRequests { get; set; }
    public long TotalBandwidth { get; set; }
    public double CacheHitRatio { get; set; }
    public Dictionary<string, long> StatusCodes { get; set; } = new();
    public string Message { get; set; }
}

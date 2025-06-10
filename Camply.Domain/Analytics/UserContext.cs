namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Kullanıcı context bilgisi
    /// </summary>
    public class UserContext
    {
        public DateTime Timestamp { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string DeviceType { get; set; }
        public string SessionId { get; set; }
        public TimeSpan SessionDuration { get; set; }
        public WeatherData Weather { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}

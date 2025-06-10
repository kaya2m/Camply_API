namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Hava durumu verisi
    /// </summary>
    public class WeatherData
    {
        public float Temperature { get; set; }
        public string Condition { get; set; } // "sunny", "rainy", "cloudy", etc.
        public float Humidity { get; set; }
        public float WindSpeed { get; set; }
        public bool IsCampingWeather { get; set; }
        public float CampingScore { get; set; } // 0-1 arası
    }
}

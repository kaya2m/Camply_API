using MongoDB.Bson;

namespace Camply.Domain.Analytics
{
    /// <summary>
    /// Kullanıcı ilgi profilleri - MongoDB
    /// </summary>
    public class UserInterestDocument
    {
        public ObjectId Id { get; set; }
        public Guid UserId { get; set; }
        public Dictionary<string, double> Interests { get; set; } = new(); // Kategori -> Skor
        public DateTime UpdatedAt { get; set; }
        public string Version { get; set; }
        public List<InterestTrend> History { get; set; } = new(); // Zaman içindeki değişim
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

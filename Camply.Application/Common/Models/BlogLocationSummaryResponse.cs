namespace Camply.Application.Common.Models
{
    /// <summary>
    /// Lokasyon özet yanıtı
    /// </summary>
    public class BlogLocationSummaryResponse
    {
        /// <summary>
        /// Lokasyon ID'si (null olabilir)
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Lokasyon adı
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Enlem
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Boylam
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Lokasyon türü
        /// </summary>
        public string Type { get; set; }
    }
}

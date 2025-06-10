using Camply.Domain.Common;

namespace Camply.Domain.MachineLearning
{
    /// <summary>
    /// İçerik feature vektörleri - Post/Blog özellikleri
    /// </summary>
    public class MLContentFeature : BaseEntity
    {
        /// <summary>
        /// İçerik ID'si (Post veya Blog)
        /// </summary>
        public Guid ContentId { get; set; }

        /// <summary>
        /// İçerik türü (Post, Blog)
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Feature vektörü (JSON format)
        /// </summary>
        public string FeatureVector { get; set; }

        /// <summary>
        /// Feature kategorisi (text, image, location, social, etc.)
        /// </summary>
        public string FeatureCategory { get; set; }

        /// <summary>
        /// Feature versiyonu
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// İçerik kategorileri (kamp, doğa, fotoğraf vs.)
        /// </summary>
        public string Categories { get; set; }

        /// <summary>
        /// İçerik kalite skoru
        /// </summary>
        public float QualityScore { get; set; }

        /// <summary>
        /// Viral potansiyel skoru
        /// </summary>
        public float? ViralPotential { get; set; }

        /// <summary>
        /// Sentiment skoru (-1 ile 1 arası)
        /// </summary>
        public float? SentimentScore { get; set; }

        /// <summary>
        /// Metadatalar
        /// </summary>
        public string Metadata { get; set; }
    }
}

using Camply.Domain.Common;

namespace Camply.Domain.MachineLearning
{
    /// <summary>
    /// Model versiyonları ve metadata
    /// </summary>
    public class MLModel : BaseEntity
    {
        /// <summary>
        /// Model adı
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Model versiyonu
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Model türü (engagement_prediction, content_embedding, etc.)
        /// </summary>
        public string ModelType { get; set; }

        /// <summary>
        /// Model dosya yolu
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// Training tarihi
        /// </summary>
        public DateTime TrainedAt { get; set; }

        /// <summary>
        /// Training dataset bilgisi
        /// </summary>
        public string TrainingDataInfo { get; set; }

        /// <summary>
        /// Model performans metrikleri (JSON)
        /// </summary>
        public string PerformanceMetrics { get; set; }

        /// <summary>
        /// Model durumu (training, active, deprecated)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Model açıklaması
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Hiperparametreler (JSON)
        /// </summary>
        public string Hyperparameters { get; set; }

        /// <summary>
        /// Model boyutu (bytes)
        /// </summary>
        public long ModelSize { get; set; }

        /// <summary>
        /// Aktif model mi?
        /// </summary>
        public bool IsActive { get; set; }
    }
}

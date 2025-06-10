using Camply.Domain.Common;

namespace Camply.Domain.MachineLearning
{
    /// <summary>
    /// Training job'ları takibi
    /// </summary>
    public class MLTrainingJob : BaseEntity
    {
        /// <summary>
        /// Job adı
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// Model ID'si
        /// </summary>
        public Guid? ModelId { get; set; }

        /// <summary>
        /// Job durumu (queued, running, completed, failed)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Başlangıç tarihi
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Bitiş tarihi
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Training parametreleri (JSON)
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Training sonuçları (JSON)
        /// </summary>
        public string Results { get; set; }

        /// <summary>
        /// Hata mesajları
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// İlerleme durumu (0-100)
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// Log dosyası yolu
        /// </summary>
        public string LogPath { get; set; }

        // Navigation Properties
        public virtual MLModel Model { get; set; }
    }
}

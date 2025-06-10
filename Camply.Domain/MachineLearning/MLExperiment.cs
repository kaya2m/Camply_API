using Camply.Domain.Common;

namespace Camply.Domain.MachineLearning
{
    /// <summary>
    /// A/B Test eksperimentleri
    /// </summary>
    public class MLExperiment : BaseEntity
    {
        /// <summary>
        /// Experiment adı
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Experiment açıklaması
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Experiment türü (algorithm_ab_test, model_comparison, etc.)
        /// </summary>
        public string ExperimentType { get; set; }

        /// <summary>
        /// Başlangıç tarihi
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Bitiş tarihi
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Experiment durumu (planning, running, completed, stopped)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Kontrol grubu yapılandırması (JSON)
        /// </summary>
        public string ControlConfig { get; set; }

        /// <summary>
        /// Test grubu yapılandırması (JSON)
        /// </summary>
        public string TestConfig { get; set; }

        /// <summary>
        /// Trafik yüzdesi (0-100)
        /// </summary>
        public int TrafficPercentage { get; set; }

        /// <summary>
        /// Hedef metrikler (JSON)
        /// </summary>
        public string TargetMetrics { get; set; }

        /// <summary>
        /// Sonuçlar (JSON)
        /// </summary>
        public string Results { get; set; }

        /// <summary>
        /// İstatistiksel anlamlılık
        /// </summary>
        public float? StatisticalSignificance { get; set; }

        /// <summary>
        /// Kazanan grup (control, test, inconclusive)
        /// </summary>
        public string Winner { get; set; }
    }
}

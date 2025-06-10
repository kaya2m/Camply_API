using Camply.Domain.Auth;
using Camply.Domain.Common;

namespace Camply.Domain.MachineLearning
{
    /// <summary>
    /// Kullanıcı experiment atamalarını takip
    /// </summary>
    public class MLUserExperiment : BaseTrackingEntity
    {
        /// <summary>
        /// Kullanıcı ID'si
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Experiment ID'si
        /// </summary>
        public Guid ExperimentId { get; set; }

        /// <summary>
        /// Atanan grup (control, test)
        /// </summary>
        public string AssignedGroup { get; set; }

        /// <summary>
        /// Atama tarihi
        /// </summary>
        public DateTime AssignedAt { get; set; }

        // Navigation Properties
        public virtual User User { get; set; }
        public virtual MLExperiment Experiment { get; set; }
    }
}

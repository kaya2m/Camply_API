using Camply.Domain.Auth;
using Camply.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.MachineLearning
{
    /// <summary>
    /// Kullanıcı feature vektörleri - ML için hesaplanmış özellikler
    /// </summary>
    public class MLUserFeature : BaseEntity
    {
        /// <summary>
        /// İlişkili kullanıcı ID'si
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Feature vektörü (JSON format)
        /// </summary>
        public string FeatureVector { get; set; }

        /// <summary>
        /// Feature tipi (behavioral, demographic, social, etc.)
        /// </summary>
        public string FeatureType { get; set; }

        /// <summary>
        /// Feature versiyonu
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Son hesaplanma tarihi
        /// </summary>
        public DateTime LastCalculated { get; set; }

        /// <summary>
        /// Feature kalitesi (0-1 arası)
        /// </summary>
        public float QualityScore { get; set; }

        /// <summary>
        /// Metadatalar
        /// </summary>
        public string Metadata { get; set; }

        // Navigation Properties
        public virtual User User { get; set; }
    }
}

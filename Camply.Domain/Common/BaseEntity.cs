using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Common
{
    /// <summary>
    /// Tüm varlıklar için temel sınıf
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Benzersiz tanımlayıcı (Primary Key)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Oluşturan kullanıcı ID
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// Son güncellenme tarihi
        /// </summary>
        public DateTime? LastModifiedAt { get; set; }

        /// <summary>
        /// Son güncelleyen kullanıcı ID
        /// </summary>
        public Guid? LastModifiedBy { get; set; }

        /// <summary>
        /// Silindi mi?
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Silinme tarihi
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Silen kullanıcı ID
        /// </summary>
        public Guid? DeletedBy { get; set; }
    }
}

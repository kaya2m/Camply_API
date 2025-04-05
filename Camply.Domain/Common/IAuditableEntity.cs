using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Common
{
    /// <summary>
    /// Denetlenebilir varlıklar için arayüz
    /// </summary>
    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; set; }
        Guid? CreatedBy { get; set; }
        DateTime? LastModifiedAt { get; set; }
        Guid? LastModifiedBy { get; set; }
    }
}

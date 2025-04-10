﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Common
{
    /// <summary>
    /// Takip edilen varlıklar için temel sınıf
    /// </summary>
    public abstract class BaseTrackingEntity
    {
        /// <summary>
        /// Benzersiz tanımlayıcı (Primary Key)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}

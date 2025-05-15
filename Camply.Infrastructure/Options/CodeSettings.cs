using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class CodeSettings
    {
        public int CodeExpirationMinutes { get; set; } = 15;
        public bool AllowMultipleCodes { get; set; } = false;
        public int MaxFailedAttempts { get; set; } = 3;
    }
}

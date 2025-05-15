using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class EmailSettings
    {
        // Common email settings
        public string SenderEmail { get; set; }
        public string SenderName { get; set; }

        // Amazon SES specific settings
        public string AwsAccessKey { get; set; }
        public string AwsSecretKey { get; set; }
        public string AwsRegion { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class AzureRedisSettings
    {
        public string ConnectionString { get; set; }
        public string InstanceName { get; set; } = "Camply";
        public int DefaultExpirationMinutes { get; set; } = 60;
        public bool AbortOnConnectFail { get; set; } = false;
        public int Database { get; set; } = 0;
        public int ConnectTimeout { get; set; } = 15000; // Azure için daha yüksek
        public int SyncTimeout { get; set; } = 15000; // Azure için daha yüksek
        public bool UseSsl { get; set; } = true; // Azure Redis SSL kullanır
        public string Password { get; set; } // Eğer connection string'de yoksa
        public int Port { get; set; } = 6380; // Azure Redis default SSL port
        public string HostName { get; set; } // Azure hostname
        public int ConnectRetry { get; set; } = 3;
        public bool AllowAdmin { get; set; } = false;
    }
}

using System;
using System.Runtime.Serialization;

namespace WatchDogs.Core
{
    public enum HealthCheckType
    {
        /// <summary>Only Windows service status (Running) is checked.</summary>
        ServiceStatus = 0,
        /// <summary>Service status + TCP connect to host:port.</summary>
        TcpPort = 1,
        /// <summary>Service status + HTTP(S) request.</summary>
        HttpUrl = 2
    }

    /// <summary>All tunable parameters. Stored as JSON in %ProgramData%\WatchDogs\config.json.</summary>
    [DataContract]
    public sealed class AppConfig
    {
        /// <summary>Windows service name of the 1C server to monitor (e.g. 1CV8S).</summary>
        [DataMember(Name = "targetServiceName")]
        public string TargetServiceName { get; set; }

        [DataMember(Name = "checkType")]
        public HealthCheckType CheckType { get; set; }

        [DataMember(Name = "tcpHost")]
        public string TcpHost { get; set; }

        [DataMember(Name = "tcpPort")]
        public int TcpPort { get; set; }

        [DataMember(Name = "httpUrl")]
        public string HttpUrl { get; set; }

        /// <summary>Interval between health checks, seconds.</summary>
        [DataMember(Name = "checkIntervalSeconds")]
        public int CheckIntervalSeconds { get; set; }

        /// <summary>Delay after system reboot / service start before monitoring begins, minutes.</summary>
        [DataMember(Name = "bootDelayMinutes")]
        public int BootDelayMinutes { get; set; }

        /// <summary>Timeout for TCP/HTTP response, seconds.</summary>
        [DataMember(Name = "responseTimeoutSeconds")]
        public int ResponseTimeoutSeconds { get; set; }

        [DataMember(Name = "autoRestart")]
        public bool AutoRestart { get; set; }

        /// <summary>Max consecutive restarts of the target service before a long cooldown.</summary>
        [DataMember(Name = "maxRestartAttempts")]
        public int MaxRestartAttempts { get; set; }

        /// <summary>Minimum pause between two restarts of the target service, minutes.</summary>
        [DataMember(Name = "restartCooldownMinutes")]
        public int RestartCooldownMinutes { get; set; }

        /// <summary>Master switch: when false the service idles but keeps running.</summary>
        [DataMember(Name = "monitoringEnabled")]
        public bool MonitoringEnabled { get; set; }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                TargetServiceName = "1CV8S",
                CheckType = HealthCheckType.ServiceStatus,
                TcpHost = "localhost",
                TcpPort = 1541,
                HttpUrl = "http://localhost/health",
                CheckIntervalSeconds = 30,
                BootDelayMinutes = 5,
                ResponseTimeoutSeconds = 10,
                AutoRestart = true,
                MaxRestartAttempts = 5,
                RestartCooldownMinutes = 5,
                MonitoringEnabled = true
            };
        }

        /// <summary>Clamp values into sane ranges after load.</summary>
        public void Normalize()
        {
            if (TargetServiceName != null)
                TargetServiceName = TargetServiceName.Trim();

            if (string.IsNullOrWhiteSpace(TcpHost))
                TcpHost = "localhost";

            if (string.IsNullOrWhiteSpace(HttpUrl))
                HttpUrl = "http://localhost/health";

            if (TcpPort < 1 || TcpPort > 65535)
                TcpPort = 1541;

            CheckIntervalSeconds = Clamp(CheckIntervalSeconds, 5, 86400, 30);
            BootDelayMinutes = Clamp(BootDelayMinutes, 0, 1440, 5);
            ResponseTimeoutSeconds = Clamp(ResponseTimeoutSeconds, 1, 300, 10);
            MaxRestartAttempts = Clamp(MaxRestartAttempts, 1, 100, 5);
            RestartCooldownMinutes = Clamp(RestartCooldownMinutes, 0, 1440, 5);

            if (!Enum.IsDefined(typeof(HealthCheckType), CheckType))
                CheckType = HealthCheckType.ServiceStatus;
        }

        private static int Clamp(int value, int min, int max, int fallback)
        {
            if (value <= 0)
                return fallback;
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}

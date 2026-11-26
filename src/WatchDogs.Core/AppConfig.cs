using System;
using System.Collections.Generic;
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

    /// <summary>Per-service monitoring parameters.</summary>
    [DataContract]
    public sealed class ServiceConfig
    {
        /// <summary>Windows service name (Service Name, not display name).</summary>
        [DataMember(Name = "targetServiceName")]
        public string TargetServiceName { get; set; }

        /// <summary>Optional human-readable alias shown in the GUI.</summary>
        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }

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

        /// <summary>Timeout for TCP/HTTP response, seconds.</summary>
        [DataMember(Name = "responseTimeoutSeconds")]
        public int ResponseTimeoutSeconds { get; set; }

        [DataMember(Name = "autoRestart")]
        public bool AutoRestart { get; set; }

        [DataMember(Name = "maxRestartAttempts")]
        public int MaxRestartAttempts { get; set; }

        [DataMember(Name = "restartCooldownMinutes")]
        public int RestartCooldownMinutes { get; set; }

        /// <summary>Per-service enable switch.</summary>
        [DataMember(Name = "monitoringEnabled")]
        public bool MonitoringEnabled { get; set; }

        public static ServiceConfig CreateDefault(string serviceName = "1CV8S")
        {
            return new ServiceConfig
            {
                TargetServiceName = serviceName,
                DisplayName = "",
                CheckType = HealthCheckType.ServiceStatus,
                TcpHost = "localhost",
                TcpPort = 1541,
                HttpUrl = "http://localhost/health",
                CheckIntervalSeconds = 30,
                ResponseTimeoutSeconds = 10,
                AutoRestart = true,
                MaxRestartAttempts = 5,
                RestartCooldownMinutes = 5,
                MonitoringEnabled = true
            };
        }

        public void Normalize()
        {
            if (TargetServiceName != null)
                TargetServiceName = TargetServiceName.Trim();
            if (DisplayName != null)
                DisplayName = DisplayName.Trim();

            if (string.IsNullOrWhiteSpace(TcpHost))
                TcpHost = "localhost";
            if (string.IsNullOrWhiteSpace(HttpUrl))
                HttpUrl = "http://localhost/health";
            if (TcpPort < 1 || TcpPort > 65535)
                TcpPort = 1541;

            CheckIntervalSeconds = Clamp(CheckIntervalSeconds, 5, 86400, 30);
            ResponseTimeoutSeconds = Clamp(ResponseTimeoutSeconds, 1, 300, 10);
            MaxRestartAttempts = Clamp(MaxRestartAttempts, 1, 100, 5);
            RestartCooldownMinutes = Clamp(RestartCooldownMinutes, 0, 1440, 5);

            if (!Enum.IsDefined(typeof(HealthCheckType), CheckType))
                CheckType = HealthCheckType.ServiceStatus;
        }

        public string Title
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(TargetServiceName) ? "Service Name не задан" : TargetServiceName;
                if (!string.IsNullOrWhiteSpace(DisplayName))
                    return DisplayName + "  (" + name + ")";
                return name;
            }
        }

        public ServiceConfig Clone()
        {
            return new ServiceConfig
            {
                TargetServiceName = TargetServiceName,
                DisplayName = DisplayName,
                CheckType = CheckType,
                TcpHost = TcpHost,
                TcpPort = TcpPort,
                HttpUrl = HttpUrl,
                CheckIntervalSeconds = CheckIntervalSeconds,
                ResponseTimeoutSeconds = ResponseTimeoutSeconds,
                AutoRestart = AutoRestart,
                MaxRestartAttempts = MaxRestartAttempts,
                RestartCooldownMinutes = RestartCooldownMinutes,
                MonitoringEnabled = MonitoringEnabled
            };
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

    /// <summary>
    /// Root configuration: global boot delay + list of monitored services.
    /// Stored as JSON in %ProgramData%\WatchDogs\config.json.
    /// Legacy single-service files are migrated on load.
    /// </summary>
    [DataContract]
    public sealed class AppConfig
    {
        /// <summary>Delay after system reboot / engine start before monitoring begins, minutes.</summary>
        [DataMember(Name = "bootDelayMinutes")]
        public int BootDelayMinutes { get; set; }

        [DataMember(Name = "services")]
        public List<ServiceConfig> Services { get; set; }

        // ----- legacy single-service fields (migration only) -----
        [DataMember(Name = "targetServiceName")]
        public string LegacyTargetServiceName { get; set; }

        [DataMember(Name = "checkType")]
        public HealthCheckType LegacyCheckType { get; set; }

        [DataMember(Name = "tcpHost")]
        public string LegacyTcpHost { get; set; }

        [DataMember(Name = "tcpPort")]
        public int LegacyTcpPort { get; set; }

        [DataMember(Name = "httpUrl")]
        public string LegacyHttpUrl { get; set; }

        [DataMember(Name = "checkIntervalSeconds")]
        public int LegacyCheckIntervalSeconds { get; set; }

        [DataMember(Name = "responseTimeoutSeconds")]
        public int LegacyResponseTimeoutSeconds { get; set; }

        [DataMember(Name = "autoRestart")]
        public bool LegacyAutoRestart { get; set; }

        [DataMember(Name = "maxRestartAttempts")]
        public int LegacyMaxRestartAttempts { get; set; }

        [DataMember(Name = "restartCooldownMinutes")]
        public int LegacyRestartCooldownMinutes { get; set; }

        [DataMember(Name = "monitoringEnabled")]
        public bool LegacyMonitoringEnabled { get; set; }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                BootDelayMinutes = 5,
                Services = new List<ServiceConfig> { ServiceConfig.CreateDefault("1CV8S") }
            };
        }

        public void Normalize()
        {
            BootDelayMinutes = Clamp(BootDelayMinutes, 0, 1440, 5);

            if (Services == null)
                Services = new List<ServiceConfig>();

            // Migrate legacy single-service config
            if (Services.Count == 0 && !string.IsNullOrWhiteSpace(LegacyTargetServiceName))
            {
                var s = ServiceConfig.CreateDefault(LegacyTargetServiceName);
                s.CheckType = LegacyCheckType;
                s.TcpHost = LegacyTcpHost;
                s.TcpPort = LegacyTcpPort;
                s.HttpUrl = LegacyHttpUrl;
                s.CheckIntervalSeconds = LegacyCheckIntervalSeconds;
                s.ResponseTimeoutSeconds = LegacyResponseTimeoutSeconds;
                s.AutoRestart = LegacyAutoRestart;
                s.MaxRestartAttempts = LegacyMaxRestartAttempts;
                s.RestartCooldownMinutes = LegacyRestartCooldownMinutes;
                s.MonitoringEnabled = LegacyMonitoringEnabled;
                Services.Add(s);
            }

            if (Services.Count == 0)
                Services.Add(ServiceConfig.CreateDefault());

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cleaned = new List<ServiceConfig>();
            foreach (var s in Services)
            {
                if (s == null)
                    continue;
                s.Normalize();
                if (string.IsNullOrWhiteSpace(s.TargetServiceName))
                    continue;
                if (!seen.Add(s.TargetServiceName))
                    continue;
                cleaned.Add(s);
            }

            if (cleaned.Count == 0)
                cleaned.Add(ServiceConfig.CreateDefault());

            Services = cleaned;
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

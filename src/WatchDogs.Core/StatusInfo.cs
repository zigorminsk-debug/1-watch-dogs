using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WatchDogs.Core
{
    /// <summary>Per-service check snapshot.</summary>
    [DataContract]
    public sealed class ServiceStatus
    {
        [DataMember(Name = "targetServiceName")]
        public string TargetServiceName { get; set; }

        [DataMember(Name = "displayName")]
        public string DisplayName { get; set; }

        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }

        [DataMember(Name = "targetServiceStatus")]
        public string TargetServiceStatus { get; set; }

        [DataMember(Name = "lastCheckHealthy")]
        public bool LastCheckHealthy { get; set; }

        [DataMember(Name = "lastCheckMessage")]
        public string LastCheckMessage { get; set; }

        [DataMember(Name = "lastCheckTimeUtc")]
        public string LastCheckTimeUtc { get; set; }

        [DataMember(Name = "totalChecks")]
        public int TotalChecks { get; set; }

        [DataMember(Name = "totalRestarts")]
        public int TotalRestarts { get; set; }

        [DataMember(Name = "consecutiveRestarts")]
        public int ConsecutiveRestarts { get; set; }

        [DataMember(Name = "lastRestartTimeUtc")]
        public string LastRestartTimeUtc { get; set; }

        [DataMember(Name = "state")]
        public string State { get; set; }
    }

    /// <summary>Engine-level snapshot written by the monitoring service for the GUI.</summary>
    [DataContract]
    public sealed class StatusInfo
    {
        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "updatedAtUtc")]
        public string UpdatedAtUtc { get; set; }

        [DataMember(Name = "services")]
        public List<ServiceStatus> Services { get; set; }

        // ----- legacy single-service fields (older GUI still reads these) -----
        [DataMember(Name = "targetServiceName")]
        public string TargetServiceName { get; set; }

        [DataMember(Name = "targetServiceStatus")]
        public string TargetServiceStatus { get; set; }

        [DataMember(Name = "lastCheckHealthy")]
        public bool LastCheckHealthy { get; set; }

        [DataMember(Name = "lastCheckMessage")]
        public string LastCheckMessage { get; set; }

        [DataMember(Name = "lastCheckTimeUtc")]
        public string LastCheckTimeUtc { get; set; }

        [DataMember(Name = "totalChecks")]
        public int TotalChecks { get; set; }

        [DataMember(Name = "totalRestarts")]
        public int TotalRestarts { get; set; }

        [DataMember(Name = "consecutiveRestarts")]
        public int ConsecutiveRestarts { get; set; }

        [DataMember(Name = "lastRestartTimeUtc")]
        public string LastRestartTimeUtc { get; set; }

        public static StatusInfo CreateIdle(string state, string message)
        {
            return new StatusInfo
            {
                State = state,
                Message = message,
                Services = new List<ServiceStatus>(),
                UpdatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        /// <summary>Sync legacy fields from the first service for backward compatibility.</summary>
        public void SyncLegacy()
        {
            if (Services == null)
            {
                Services = new List<ServiceStatus>();
                return;
            }

            if (Services.Count > 0)
            {
                var s = Services[0];
                TargetServiceName = s.TargetServiceName;
                TargetServiceStatus = s.TargetServiceStatus;
                LastCheckHealthy = s.LastCheckHealthy;
                LastCheckMessage = s.LastCheckMessage;
                LastCheckTimeUtc = s.LastCheckTimeUtc;
                TotalChecks = s.TotalChecks;
                TotalRestarts = s.TotalRestarts;
                ConsecutiveRestarts = s.ConsecutiveRestarts;
                LastRestartTimeUtc = s.LastRestartTimeUtc;
            }

            UpdatedAtUtc = DateTime.UtcNow.ToString("o");
        }
    }
}

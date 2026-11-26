using System;
using System.Runtime.Serialization;

namespace WatchDogs.Core
{
    /// <summary>Snapshot written by the monitoring service for the GUI to display.</summary>
    [DataContract]
    public sealed class StatusInfo
    {
        [DataMember(Name = "state")]
        public string State { get; set; }

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

        [DataMember(Name = "updatedAtUtc")]
        public string UpdatedAtUtc { get; set; }

        public static StatusInfo CreateIdle(string state, string message)
        {
            var now = DateTime.UtcNow.ToString("o");
            return new StatusInfo
            {
                State = state,
                LastCheckMessage = message,
                UpdatedAtUtc = now
            };
        }
    }
}

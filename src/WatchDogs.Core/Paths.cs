using System;
using System.IO;

namespace WatchDogs.Core
{
    /// <summary>Well-known filesystem locations shared by GUI and Windows service.</summary>
    public static class Paths
    {
        public const string MonitoringServiceName = "WatchDogsMonitor";
        public const string MonitoringServiceDisplayName = "Watch Dogs Monitor";

        public static string BaseDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "WatchDogs");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(BaseDirectory, "config.json"); }
        }

        public static string StatusPath
        {
            get { return Path.Combine(BaseDirectory, "status.json"); }
        }

        public static string LogDirectory
        {
            get { return Path.Combine(BaseDirectory, "logs"); }
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(BaseDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }
}

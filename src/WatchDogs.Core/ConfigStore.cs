using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace WatchDogs.Core
{
    /// <summary>Atomic JSON load/save of configuration and status files.</summary>
    public static class ConfigStore
    {
        private static readonly object Gate = new object();

        public static AppConfig LoadConfig()
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(Paths.ConfigPath))
                    {
                        var fresh = AppConfig.CreateDefault();
                        fresh.Normalize();
                        SaveConfig(fresh);
                        return fresh;
                    }

                    var cfg = ReadJson<AppConfig>(Paths.ConfigPath) ?? AppConfig.CreateDefault();
                    cfg.Normalize();
                    return cfg;
                }
                catch
                {
                    var fallback = AppConfig.CreateDefault();
                    fallback.Normalize();
                    return fallback;
                }
            }
        }

        public static void SaveConfig(AppConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            config.Normalize();
            Paths.EnsureDirectories();

            lock (Gate)
            {
                WriteJsonAtomic(Paths.ConfigPath, config);
            }
        }

        public static StatusInfo LoadStatus()
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(Paths.StatusPath))
                        return null;
                    return ReadJson<StatusInfo>(Paths.StatusPath);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void SaveStatus(StatusInfo status)
        {
            if (status == null)
                return;

            status.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            Paths.EnsureDirectories();

            lock (Gate)
            {
                WriteJsonAtomic(Paths.StatusPath, status);
            }
        }

        private static T ReadJson<T>(string path) where T : class
        {
            using (var stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(stream);
            }
        }

        private static void WriteJsonAtomic<T>(string path, T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            var tmp = path + ".tmp";

            using (var stream = File.Create(tmp))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
            {
                serializer.WriteObject(writer, value);
                writer.Flush();
            }

            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }
    }
}

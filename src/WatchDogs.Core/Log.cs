using System;
using System.IO;
using System.Text;

namespace WatchDogs.Core
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    /// <summary>Simple thread-safe file logger. One file per day under %ProgramData%\WatchDogs\logs.</summary>
    public static class Log
    {
        private static readonly object Gate = new object();

        public static bool DebugEnabled { get; set; }

        public static void Debug(string message)
        {
            if (DebugEnabled)
                Write(LogLevel.Debug, message, null);
        }

        public static void Info(string message)
        {
            Write(LogLevel.Info, message, null);
        }

        public static void Warn(string message)
        {
            Write(LogLevel.Warn, message, null);
        }

        public static void Error(string message)
        {
            Write(LogLevel.Error, message, null);
        }

        public static void Error(string message, Exception ex)
        {
            Write(LogLevel.Error, message, ex);
        }

        public static string GetLogFilePath()
        {
            return Path.Combine(Paths.LogDirectory, "watchdogs-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        }

        /// <summary>Return up to <paramref name="maxLines"/> trailing lines of the newest log file.</summary>
        public static string ReadTail(int maxLines)
        {
            try
            {
                if (!Directory.Exists(Paths.LogDirectory))
                    return string.Empty;

                var files = Directory.GetFiles(Paths.LogDirectory, "watchdogs-*.log");
                if (files.Length == 0)
                    return string.Empty;

                Array.Sort(files, StringComparer.Ordinal);
                var newest = files[files.Length - 1];

                var lines = File.ReadAllLines(newest, Encoding.UTF8);
                var start = Math.Max(0, lines.Length - maxLines);
                var sb = new StringBuilder();
                for (var i = start; i < lines.Length; i++)
                {
                    sb.AppendLine(lines[i]);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "Не удалось прочитать журнал: " + ex.Message;
            }
        }

        private static void Write(LogLevel level, string message, Exception ex)
        {
            if (level == LogLevel.Debug && !DebugEnabled)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ");
                sb.Append(message ?? string.Empty);
                if (ex != null)
                {
                    sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                    if (ex.StackTrace != null)
                        sb.AppendLine().Append(ex.StackTrace);
                }

                var line = sb.ToString();
                var path = GetLogFilePath();

                lock (Gate)
                {
                    Directory.CreateDirectory(Paths.LogDirectory);
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never crash the service.
            }
        }
    }
}

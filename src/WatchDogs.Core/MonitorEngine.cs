using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace WatchDogs.Core
{
    /// <summary>
    /// Core monitoring loop shared by the Windows service and console mode.
    /// - Waits BootDelayMinutes after start (system reboot scenario)
    /// - Periodically checks target 1C service health
    /// - Restarts it when unhealthy and AutoRestart is enabled
    /// </summary>
    public sealed class MonitorEngine
    {
        private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(false);
        private int _totalChecks;
        private int _totalRestarts;
        private int _consecutiveRestarts;
        private DateTime _lastRestartUtc = DateTime.MinValue;
        private StatusInfo _status = new StatusInfo { State = "Init" };

        public void RequestStop()
        {
            _stopEvent.Set();
        }

        /// <summary>Blocking run until RequestStop() or process exit.</summary>
        public void Run()
        {
            Paths.EnsureDirectories();
            Log.Info("=== Watch Dogs monitor started (PID " + Process.GetCurrentProcess().Id + ") ===");
            Log.Info("Базовая директория: " + Paths.BaseDirectory);

            var config = ConfigStore.LoadConfig();
            Log.Info("Конфигурация загружена. Служба 1С: '" + config.TargetServiceName +
                     "', интервал: " + config.CheckIntervalSeconds + " с," +
                     " задержка после загрузки: " + config.BootDelayMinutes + " мин," +
                     " тип проверки: " + config.CheckType);

            // --- Boot delay (default 5 minutes after reboot) ---
            if (config.BootDelayMinutes > 0)
            {
                var delay = TimeSpan.FromMinutes(config.BootDelayMinutes);
                Log.Info("Ожидание " + config.BootDelayMinutes +
                         " мин перед началом мониторинга (после загрузки системы)...");

                _status.State = "WaitingBootDelay";
                _status.LastCheckMessage = "Ожидание " + config.BootDelayMinutes + " мин после загрузки системы";
                _status.TargetServiceName = config.TargetServiceName;
                PublishStatus();

                if (_stopEvent.Wait(delay))
                {
                    Log.Info("Мониторинг остановлен во время задержки после загрузки.");
                    return;
                }
            }

            Log.Info("Начало мониторинга службы '" + config.TargetServiceName + "'");

            while (!_stopEvent.IsSet)
            {
                try
                {
                    config = ConfigStore.LoadConfig();

                    if (!config.MonitoringEnabled)
                    {
                        _status.State = "Disabled";
                        _status.LastCheckMessage = "Мониторинг отключён в настройках";
                        _status.TargetServiceName = config.TargetServiceName;
                        PublishStatus();
                        Log.Debug("Мониторинг отключён, ожидание...");

                        if (_stopEvent.Wait(TimeSpan.FromSeconds(Math.Max(5, config.CheckIntervalSeconds))))
                            break;
                        continue;
                    }

                    Step(config);
                }
                catch (Exception ex)
                {
                    Log.Error("Ошибка цикла мониторинга", ex);
                    _status.State = "Error";
                    _status.LastCheckMessage = "Ошибка: " + ex.Message;
                    PublishStatus();
                }

                var interval = TimeSpan.FromSeconds(Math.Max(5, config.CheckIntervalSeconds));
                if (_stopEvent.Wait(interval))
                    break;
            }

            Log.Info("=== Watch Dogs monitor stopped ===");
        }

        private void Step(AppConfig config)
        {
            _totalChecks++;

            var result = HealthChecker.Check(config);
            var nowUtc = DateTime.UtcNow;

            _status.State = "Monitoring";
            _status.TargetServiceName = config.TargetServiceName;
            _status.TargetServiceStatus = result.ServiceStatus;
            _status.LastCheckHealthy = result.IsHealthy;
            _status.LastCheckMessage = result.Message;
            _status.LastCheckTimeUtc = nowUtc.ToString("o");
            _status.TotalChecks = _totalChecks;
            _status.TotalRestarts = _totalRestarts;
            _status.ConsecutiveRestarts = _consecutiveRestarts;

            if (result.IsHealthy)
            {
                if (_consecutiveRestarts > 0)
                    Log.Info("Служба 1С снова здорова. Счётчик перезапусков сброшен.");
                _consecutiveRestarts = 0;
                Log.Debug("Проверка #" + _totalChecks + ": OK — " + result.Message);
                PublishStatus();
                return;
            }

            Log.Warn("Проверка #" + _totalChecks + ": " + result.Message);
            PublishStatus();

            if (!config.AutoRestart)
            {
                Log.Warn("Автоперезапуск отключён — действие не предпринимается.");
                return;
            }

            // Cooldown between restarts
            var cooldown = TimeSpan.FromMinutes(Math.Max(0, config.RestartCooldownMinutes));
            var sinceLast = nowUtc - _lastRestartUtc;
            if (_lastRestartUtc != DateTime.MinValue && sinceLast < cooldown)
            {
                var remaining = cooldown - sinceLast;
                Log.Warn(string.Format(
                    "Перезапуск отложен: пауза между перезапусками ещё {0:hh\\:mm\\:ss}",
                    remaining));
                return;
            }

            // Max consecutive restarts → long back-off (do not thrash the 1C server)
            if (_consecutiveRestarts >= config.MaxRestartAttempts)
            {
                var backoff = TimeSpan.FromMinutes(Math.Max(1, config.RestartCooldownMinutes) * config.MaxRestartAttempts);
                Log.Error(string.Format(
                    "Достигнут лимит последовательных перезапусков ({0}). Следующая попытка через {1:hh\\:mm\\:ss}.",
                    config.MaxRestartAttempts, backoff));

                _status.State = "Backoff";
                _status.LastCheckMessage = string.Format(
                    "Лимит перезапусков ({0}) исчерпан, следующая попытка через {1:hh\\:mm\\:ss}",
                    config.MaxRestartAttempts, backoff);
                PublishStatus();

                if (_stopEvent.Wait(backoff))
                    return;

                _consecutiveRestarts = 0;
                return;
            }

            // Restart
            string restartMessage;
            var ok = HealthChecker.TryRestart(config, out restartMessage);
            _lastRestartUtc = DateTime.UtcNow;
            _status.LastRestartTimeUtc = _lastRestartUtc.ToString("o");

            if (ok)
            {
                _totalRestarts++;
                _consecutiveRestarts++;
                _status.TotalRestarts = _totalRestarts;
                _status.ConsecutiveRestarts = _consecutiveRestarts;
                _status.State = "Restarted";
                _status.LastCheckMessage = restartMessage;
                Log.Info(string.Format("Перезапуск #{0} выполнен. Подряд: {1}.",
                    _totalRestarts, _consecutiveRestarts));
            }
            else
            {
                _consecutiveRestarts++;
                _status.ConsecutiveRestarts = _consecutiveRestarts;
                _status.State = "RestartFailed";
                _status.LastCheckMessage = restartMessage;
                Log.Error("Перезапуск не удался: " + restartMessage);
            }

            PublishStatus();
        }

        private void PublishStatus()
        {
            try
            {
                ConfigStore.SaveStatus(_status);
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось записать status.json", ex);
            }
        }
    }

    /// <summary>Windows Service wrapper around <see cref="MonitorEngine"/>.</summary>
    public sealed class MonitorService : ServiceBase
    {
        private MonitorEngine _engine;
        private Thread _thread;

        public MonitorService()
        {
            ServiceName = Paths.MonitoringServiceName;
            DisplayName = Paths.MonitoringServiceDisplayName;
            CanStop = true;
            CanPauseAndContinue = false;
            CanShutdown = true;
            AutoLog = true;
            Description = "Мониторинг состояния службы 1С-сервера и автоматический перезапуск при отказе (Watch Dogs).";
        }

        protected override void OnStart(string[] args)
        {
            _engine = new MonitorEngine();
            _thread = new Thread(_engine.Run)
            {
                IsBackground = true,
                Name = "WatchDogsMonitorLoop"
            };
            _thread.Start();
        }

        protected override void OnStop()
        {
            if (_engine != null)
                _engine.RequestStop();
            if (_thread != null && _thread.IsAlive)
                _thread.Join(TimeSpan.FromSeconds(30));
        }

        protected override void OnShutdown()
        {
            OnStop();
        }
    }

    /// <summary>Install / uninstall the monitoring Windows service via sc.exe.</summary>
    public static class ServiceInstallerHelper
    {
        public static bool IsInstalled()
        {
            try
            {
                using (var sc = new ServiceController(Paths.MonitoringServiceName))
                {
                    var _ = sc.Status;
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static string GetStatusText()
        {
            try
            {
                using (var sc = new ServiceController(Paths.MonitoringServiceName))
                {
                    return sc.Status.ToString();
                }
            }
            catch (InvalidOperationException)
            {
                return "NOT_INSTALLED";
            }
            catch
            {
                return "ERROR";
            }
        }

        public static bool Install(out string message)
        {
            message = null;
            try
            {
                if (IsInstalled())
                {
                    message = "Служба уже установлена";
                    return true;
                }

                var exe = GetServiceExecutablePath();
                // sc.exe requires a space after '=' in arguments.
                var args = string.Format(
                    "create {0} binPath= \"{1}\" start= auto DisplayName= \"{2}\"",
                    Paths.MonitoringServiceName, exe, Paths.MonitoringServiceDisplayName);

                var result = RunSc(args);
                if (result != 0)
                {
                    message = "sc create завершился с кодом " + result;
                    return false;
                }

                RunSc(string.Format("description {0} \"Мониторинг службы 1С (Watch Dogs)\"", Paths.MonitoringServiceName));
                message = "Служба установлена";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static bool Uninstall(out string message)
        {
            message = null;
            try
            {
                if (!IsInstalled())
                {
                    message = "Служба не установлена";
                    return true;
                }

                try
                {
                    using (var sc = new ServiceController(Paths.MonitoringServiceName))
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped && sc.CanStop)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Не удалось остановить службу перед удалением: " + ex.Message);
                }

                var result = RunSc("delete " + Paths.MonitoringServiceName);
                if (result != 0)
                {
                    message = "sc delete завершился с кодом " + result;
                    return false;
                }

                message = "Служба удалена";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static bool Start(out string message)
        {
            return ControlService(true, out message);
        }

        public static bool Stop(out string message)
        {
            return ControlService(false, out message);
        }

        private static bool ControlService(bool start, out string message)
        {
            message = null;
            try
            {
                if (!IsInstalled())
                {
                    message = "Служба не установлена";
                    return false;
                }

                using (var sc = new ServiceController(Paths.MonitoringServiceName))
                {
                    if (start)
                    {
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            message = "Служба уже запущена";
                            return true;
                        }
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        message = "Служба запущена";
                    }
                    else
                    {
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            message = "Служба уже остановлена";
                            return true;
                        }
                        if (!sc.CanStop)
                        {
                            message = "Слубу нельзя остановить";
                            return false;
                        }
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        message = "Служба остановлена";
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static string GetServiceExecutablePath()
        {
            // Prefer the dedicated service exe if present next to current assembly.
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var candidate = Path.Combine(dir, "WatchDogs.Service.exe");
            if (File.Exists(candidate))
                return candidate;
            return Process.GetCurrentProcess().MainModule != null
                ? Process.GetCurrentProcess().MainModule.FileName
                : candidate;
        }

        private static int RunSc(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var p = Process.Start(psi))
            {
                if (p == null)
                    return -1;
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(15000);
                if (!string.IsNullOrWhiteSpace(stdout))
                    Log.Debug("sc: " + stdout.Trim());
                if (!string.IsNullOrWhiteSpace(stderr))
                    Log.Debug("sc err: " + stderr.Trim());
                return p.ExitCode;
            }
        }
    }
}

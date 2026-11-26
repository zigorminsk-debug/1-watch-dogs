using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WatchDogs.Core
{
    /// <summary>
    /// Core monitoring loop shared by the Windows service and console mode.
    /// Supports multiple target services, each with its own parameters.
    /// </summary>
    public sealed class MonitorEngine
    {
        private sealed class ServiceRuntime
        {
            public int TotalChecks;
            public int TotalRestarts;
            public int ConsecutiveRestarts;
            public DateTime LastRestartUtc = DateTime.MinValue;
            public DateTime NextDueUtc = DateTime.MinValue;
            public ServiceStatus Status = new ServiceStatus();
        }

        private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(false);
        private readonly Dictionary<string, ServiceRuntime> _runtimes =
            new Dictionary<string, ServiceRuntime>(StringComparer.OrdinalIgnoreCase);

        private string _engineState = "Init";
        private string _engineMessage = "";

        public void RequestStop()
        {
            _stopEvent.Set();
        }

        public void Run()
        {
            Paths.EnsureDirectories();
            Log.Info("=== Watch Dogs monitor started (PID " + Process.GetCurrentProcess().Id + ") ===");
            Log.Info("Базовая директория: " + Paths.BaseDirectory);

            var config = ConfigStore.LoadConfig();
            Log.Info("Конфигурация загружена. Служб в списке: " + config.Services.Count +
                     ", задержка после загрузки: " + config.BootDelayMinutes + " мин");

            foreach (var s in config.Services)
                Log.Info("  · '" + s.TargetServiceName + "' enabled=" + s.MonitoringEnabled +
                         " interval=" + s.CheckIntervalSeconds + "s check=" + s.CheckType);

            // --- Boot delay (after system reboot) ---
            if (config.BootDelayMinutes > 0)
            {
                var delay = TimeSpan.FromMinutes(config.BootDelayMinutes);
                Log.Info("Ожидание " + config.BootDelayMinutes +
                         " мин перед началом мониторинга (после загрузки системы)...");

                _engineState = "WaitingBootDelay";
                _engineMessage = "Ожидание " + config.BootDelayMinutes + " мин после загрузки системы";
                Publish(config, null);

                if (_stopEvent.Wait(delay))
                {
                    Log.Info("Мониторинг остановлен во время задержки после загрузки.");
                    return;
                }
            }

            Log.Info("Начало мониторинга (" + config.Services.Count + " служб)");

            while (!_stopEvent.IsSet)
            {
                try
                {
                    config = ConfigStore.LoadConfig();
                    Tick(config);
                }
                catch (Exception ex)
                {
                    Log.Error("Ошибка цикла мониторинга", ex);
                    _engineState = "Error";
                    _engineMessage = "Ошибка: " + ex.Message;
                    Publish(config, null);
                }

                // Fine-grained tick so per-service intervals are respected
                if (_stopEvent.Wait(TimeSpan.FromSeconds(2)))
                    break;
            }

            Log.Info("=== Watch Dogs monitor stopped ===");
        }

        private void Tick(AppConfig config)
        {
            var now = DateTime.UtcNow;
            var anyEnabled = false;
            ServiceStatus changed = null;

            foreach (var svc in config.Services)
            {
                if (svc == null || string.IsNullOrWhiteSpace(svc.TargetServiceName))
                    continue;

                var rt = GetRuntime(svc.TargetServiceName);
                rt.Status.TargetServiceName = svc.TargetServiceName;
                rt.Status.DisplayName = svc.DisplayName;
                rt.Status.Enabled = svc.MonitoringEnabled;
                rt.Status.TotalChecks = rt.TotalChecks;
                rt.Status.TotalRestarts = rt.TotalRestarts;
                rt.Status.ConsecutiveRestarts = rt.ConsecutiveRestarts;
                rt.Status.LastRestartTimeUtc =
                    rt.LastRestartUtc == DateTime.MinValue ? null : rt.LastRestartUtc.ToString("o");

                if (!svc.MonitoringEnabled)
                {
                    rt.Status.State = "Disabled";
                    rt.Status.LastCheckMessage = "Мониторинг этой службы отключён";
                    continue;
                }

                anyEnabled = true;

                // Respect per-service interval
                if (rt.NextDueUtc != DateTime.MinValue && now < rt.NextDueUtc)
                    continue;

                rt.NextDueUtc = now + TimeSpan.FromSeconds(Math.Max(5, svc.CheckIntervalSeconds));
                changed = Step(svc, rt, now);
            }

            _engineState = anyEnabled ? "Monitoring" : "Disabled";
            _engineMessage = anyEnabled
                ? "Мониторинг: " + config.Services.Count(s => s != null && s.MonitoringEnabled) + " служб"
                : "Все службы отключены в настройках";

            Publish(config, changed);
        }

        private ServiceStatus Step(ServiceConfig svc, ServiceRuntime rt, DateTime nowUtc)
        {
            rt.TotalChecks++;
            var result = HealthChecker.Check(svc);

            rt.Status.State = "Monitoring";
            rt.Status.TargetServiceStatus = result.ServiceStatus;
            rt.Status.LastCheckHealthy = result.IsHealthy;
            rt.Status.LastCheckMessage = result.Message;
            rt.Status.LastCheckTimeUtc = nowUtc.ToString("o");
            rt.Status.TotalChecks = rt.TotalChecks;
            rt.Status.TotalRestarts = rt.TotalRestarts;
            rt.Status.ConsecutiveRestarts = rt.ConsecutiveRestarts;

            if (result.IsHealthy)
            {
                if (rt.ConsecutiveRestarts > 0)
                    Log.Info("Служба '" + svc.TargetServiceName + "' снова здорова. Счётчик сброшен.");
                rt.ConsecutiveRestarts = 0;
                rt.Status.ConsecutiveRestarts = 0;
                Log.Debug("[" + svc.TargetServiceName + "] OK — " + result.Message);
                return rt.Status;
            }

            Log.Warn("[" + svc.TargetServiceName + "] " + result.Message);

            if (!svc.AutoRestart)
            {
                rt.Status.State = "Unhealthy";
                Log.Warn("[" + svc.TargetServiceName + "] автоперезапуск отключён.");
                return rt.Status;
            }

            var cooldown = TimeSpan.FromMinutes(Math.Max(0, svc.RestartCooldownMinutes));
            if (rt.LastRestartUtc != DateTime.MinValue && nowUtc - rt.LastRestartUtc < cooldown)
            {
                var remaining = cooldown - (nowUtc - rt.LastRestartUtc);
                rt.Status.State = "Cooldown";
                rt.Status.LastCheckMessage = result.Message + " (перезапуск через " +
                                              remaining.ToString(@"hh\:mm\:ss") + ")";
                Log.Warn("[" + svc.TargetServiceName + "] перезапуск отложен: " +
                         remaining.ToString(@"hh\:mm\:ss"));
                return rt.Status;
            }

            if (rt.ConsecutiveRestarts >= svc.MaxRestartAttempts)
            {
                var backoff = TimeSpan.FromMinutes(
                    Math.Max(1, svc.RestartCooldownMinutes) * Math.Max(1, svc.MaxRestartAttempts));
                rt.Status.State = "Backoff";
                rt.Status.LastCheckMessage = "Лимит перезапусков (" + svc.MaxRestartAttempts +
                                              "), следующая попытка через " + backoff.ToString(@"hh\:mm\:ss");
                Log.Error("[" + svc.TargetServiceName + "] лимит перезапусков исчерпан, back-off " +
                          backoff.ToString(@"hh\:mm\:ss"));

                // Schedule next attempt after back-off instead of blocking the whole engine
                rt.NextDueUtc = nowUtc + backoff;
                rt.ConsecutiveRestarts = 0;
                rt.Status.ConsecutiveRestarts = 0;
                return rt.Status;
            }

            string restartMessage;
            var ok = HealthChecker.TryRestart(svc, out restartMessage);
            rt.LastRestartUtc = DateTime.UtcNow;
            rt.Status.LastRestartTimeUtc = rt.LastRestartUtc.ToString("o");

            if (ok)
            {
                rt.TotalRestarts++;
                rt.ConsecutiveRestarts++;
                rt.Status.State = "Restarted";
                rt.Status.LastCheckMessage = restartMessage;
                Log.Info(string.Format("[{0}] перезапуск #{1} выполнен (подряд: {2}).",
                    svc.TargetServiceName, rt.TotalRestarts, rt.ConsecutiveRestarts));
            }
            else
            {
                rt.ConsecutiveRestarts++;
                rt.Status.State = "RestartFailed";
                rt.Status.LastCheckMessage = restartMessage;
                Log.Error("[" + svc.TargetServiceName + "] перезапуск не удался: " + restartMessage);
            }

            rt.Status.TotalRestarts = rt.TotalRestarts;
            rt.Status.ConsecutiveRestarts = rt.ConsecutiveRestarts;
            return rt.Status;
        }

        private ServiceRuntime GetRuntime(string serviceName)
        {
            ServiceRuntime rt;
            if (!_runtimes.TryGetValue(serviceName, out rt))
            {
                rt = new ServiceRuntime();
                _runtimes[serviceName] = rt;
            }
            return rt;
        }

        private void Publish(AppConfig config, ServiceStatus changed)
        {
            try
            {
                var status = new StatusInfo
                {
                    State = _engineState,
                    Message = _engineMessage,
                    Services = new List<ServiceStatus>()
                };

                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (config != null && config.Services != null)
                {
                    foreach (var svc in config.Services)
                    {
                        if (svc == null || string.IsNullOrWhiteSpace(svc.TargetServiceName))
                            continue;
                        if (!names.Add(svc.TargetServiceName))
                            continue;

                        ServiceRuntime rt;
                        if (_runtimes.TryGetValue(svc.TargetServiceName, out rt))
                        {
                            status.Services.Add(rt.Status);
                        }
                        else
                        {
                            status.Services.Add(new ServiceStatus
                            {
                                TargetServiceName = svc.TargetServiceName,
                                DisplayName = svc.DisplayName,
                                Enabled = svc.MonitoringEnabled,
                                State = svc.MonitoringEnabled ? "Pending" : "Disabled",
                                TargetServiceStatus = "?",
                                LastCheckMessage = "Ожидание первой проверки"
                            });
                        }
                    }
                }

                status.SyncLegacy();
                ConfigStore.SaveStatus(status);
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось записать status.json", ex);
            }
        }
    }

    /// <summary>Windows Service wrapper around <see cref="MonitorEngine"/>.</summary>
    public sealed class MonitorService : System.ServiceProcess.ServiceBase
    {
        private MonitorEngine _engine;
        private Thread _thread;

        public MonitorService()
        {
            // DisplayName / Description are set at install time via sc.exe.
            ServiceName = Paths.MonitoringServiceName;
            CanStop = true;
            CanPauseAndContinue = false;
            CanShutdown = true;
            AutoLog = true;
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
                using (var sc = new System.ServiceProcess.ServiceController(Paths.MonitoringServiceName))
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
                using (var sc = new System.ServiceProcess.ServiceController(Paths.MonitoringServiceName))
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
                var args = string.Format(
                    "create {0} binPath= \"{1}\" start= auto DisplayName= \"{2}\"",
                    Paths.MonitoringServiceName, exe, Paths.MonitoringServiceDisplayName);

                var result = RunSc(args);
                if (result != 0)
                {
                    message = "sc create завершился с кодом " + result;
                    return false;
                }

                RunSc(string.Format(
                    "description {0} \"Мониторинг служб (Watch Dogs)\"",
                    Paths.MonitoringServiceName));
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
                    using (var sc = new System.ServiceProcess.ServiceController(Paths.MonitoringServiceName))
                    {
                        if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Stopped && sc.CanStop)
                        {
                            sc.Stop();
                            sc.WaitForStatus(
                                System.ServiceProcess.ServiceControllerStatus.Stopped,
                                TimeSpan.FromSeconds(30));
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

                using (var sc = new System.ServiceProcess.ServiceController(Paths.MonitoringServiceName))
                {
                    if (start)
                    {
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                        {
                            message = "Служба уже запущена";
                            return true;
                        }
                        sc.Start();
                        sc.WaitForStatus(
                            System.ServiceProcess.ServiceControllerStatus.Running,
                            TimeSpan.FromSeconds(30));
                        message = "Служба запущена";
                    }
                    else
                    {
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                        {
                            message = "Служба уже остановлена";
                            return true;
                        }
                        if (!sc.CanStop)
                        {
                            message = "Службу нельзя остановить";
                            return false;
                        }
                        sc.Stop();
                        sc.WaitForStatus(
                            System.ServiceProcess.ServiceControllerStatus.Stopped,
                            TimeSpan.FromSeconds(30));
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
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            var candidate = System.IO.Path.Combine(dir, "WatchDogs.Service.exe");
            if (System.IO.File.Exists(candidate))
                return candidate;
            var p = Process.GetCurrentProcess().MainModule;
            return p != null ? p.FileName : candidate;
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

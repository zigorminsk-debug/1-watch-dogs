using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace WatchDogs.Core
{
    public sealed class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public string Message { get; set; }
        public string ServiceStatus { get; set; }
    }

    /// <summary>
    /// Checks whether the target 1C Windows service exists, is Running,
    /// and (optionally) responds on TCP port / HTTP endpoint.
    /// </summary>
    public static class HealthChecker
    {
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        public static HealthCheckResult Check(AppConfig config)
        {
            if (config == null)
                return new HealthCheckResult { IsHealthy = false, Message = "Конфигурация не задана" };

            if (string.IsNullOrWhiteSpace(config.TargetServiceName))
                return new HealthCheckResult { IsHealthy = false, Message = "Не задано имя службы 1С" };

            // 1) Service existence + status
            ServiceController controller;
            try
            {
                controller = new ServiceController(config.TargetServiceName);
                // Touch Status to force query; throws if service does not exist.
                var status = controller.Status;
                var statusText = StatusToString(status);

                if (status != ServiceControllerStatus.Running)
                {
                    controller.Dispose();
                    return new HealthCheckResult
                    {
                        IsHealthy = false,
                        ServiceStatus = statusText,
                        Message = "Служба 1С не в состоянии Running: " + statusText
                    };
                }

                controller.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    ServiceStatus = "NOT_FOUND",
                    Message = "Служба '" + config.TargetServiceName + "' не найдена: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    ServiceStatus = "ERROR",
                    Message = "Ошибка запроса состояния службы: " + ex.Message
                };
            }

            // 2) Optional deeper "responds" checks
            switch (config.CheckType)
            {
                case HealthCheckType.TcpPort:
                    return CheckTcp(config);
                case HealthCheckType.HttpUrl:
                    return CheckHttp(config);
                default:
                    return new HealthCheckResult
                    {
                        IsHealthy = true,
                        ServiceStatus = "Running",
                        Message = "Служба 1С работает (Running)"
                    };
            }
        }

        /// <summary>
        /// Restart the target service: Stop (if needed) then Start.
        /// Returns true when the service ends up Running.
        /// </summary>
        public static bool TryRestart(AppConfig config, out string message)
        {
            message = null;
            if (config == null || string.IsNullOrWhiteSpace(config.TargetServiceName))
            {
                message = "Имя службы 1С не задано";
                return false;
            }

            try
            {
                using (var sc = new ServiceController(config.TargetServiceName))
                {
                    Log.Info("Перезапуск службы '" + config.TargetServiceName + "' (текущее состояние: " +
                             StatusToString(sc.Status) + ")...");

                    if (sc.Status != ServiceControllerStatus.Stopped &&
                        sc.Status != ServiceControllerStatus.StopPending)
                    {
                        if (!sc.CanStop)
                        {
                            message = "Службу нельзя остановить (CanStop=false), состояние: " + StatusToString(sc.Status);
                            Log.Error(message);
                            return false;
                        }

                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                    }
                    else if (sc.Status == ServiceControllerStatus.StopPending)
                    {
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                    }

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(90));

                    message = "Служба '" + config.TargetServiceName + "' перезапущена и находится в состоянии Running";
                    Log.Info(message);
                    return true;
                }
            }
            catch (InvalidOperationException ex)
            {
                message = "Не удалось перезапустить службу: " + ex.Message;
                Log.Error(message, ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                message = "Таймаут ожидания состояния службы: " + ex.Message;
                Log.Error(message, ex);
                return false;
            }
            catch (Exception ex)
            {
                message = "Ошибка перезапуска службы: " + ex.Message;
                Log.Error(message, ex);
                return false;
            }
        }

        /// <summary>List all Windows service names (for GUI combo box).</summary>
        public static string[] GetInstalledServiceNames()
        {
            try
            {
                var services = ServiceController.GetServices();
                var names = new string[services.Length];
                for (var i = 0; i < services.Length; i++)
                {
                    names[i] = services[i].ServiceName;
                    services[i].Dispose();
                }
                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                return names;
            }
            catch
            {
                return new string[0];
            }
        }

        public static string GetServiceStatusText(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                return "NOT_SET";
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    return StatusToString(sc.Status);
                }
            }
            catch (InvalidOperationException)
            {
                return "NOT_FOUND";
            }
            catch
            {
                return "ERROR";
            }
        }

        private static HealthCheckResult CheckTcp(AppConfig config)
        {
            var timeoutMs = Math.Max(1, config.ResponseTimeoutSeconds) * 1000;
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var ar = client.BeginConnect(config.TcpHost, config.TcpPort, null, null);
                    var ok = ar.AsyncWaitHandle.WaitOne(timeoutMs, false);
                    if (!ok)
                    {
                        try { client.Close(); } catch { /* ignore */ }
                        return new HealthCheckResult
                        {
                            IsHealthy = false,
                            ServiceStatus = "Running",
                            Message = string.Format("TCP {0}:{1} не отвечает за {2} с",
                                config.TcpHost, config.TcpPort, config.ResponseTimeoutSeconds)
                        };
                    }

                    try
                    {
                        client.EndConnect(ar);
                    }
                    catch (Exception ex)
                    {
                        return new HealthCheckResult
                        {
                            IsHealthy = false,
                            ServiceStatus = "Running",
                            Message = string.Format("TCP {0}:{1} недоступен: {2}",
                                config.TcpHost, config.TcpPort, ex.Message)
                        };
                    }

                    if (!client.Connected)
                    {
                        return new HealthCheckResult
                        {
                            IsHealthy = false,
                            ServiceStatus = "Running",
                            Message = string.Format("TCP {0}:{1} не подключён", config.TcpHost, config.TcpPort)
                        };
                    }
                }

                return new HealthCheckResult
                {
                    IsHealthy = true,
                    ServiceStatus = "Running",
                    Message = string.Format("Служба Running, TCP {0}:{1} отвечает", config.TcpHost, config.TcpPort)
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    ServiceStatus = "Running",
                    Message = string.Format("Ошибка TCP-проверки {0}:{1}: {2}",
                        config.TcpHost, config.TcpPort, ex.Message)
                };
            }
        }

        private static HealthCheckResult CheckHttp(AppConfig config)
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(1, config.ResponseTimeoutSeconds));
            try
            {
                using (var cts = new CancellationTokenSource(timeout))
                {
                    var task = Http.GetAsync(config.HttpUrl, cts.Token);
                    if (!task.Wait(timeout))
                    {
                        return new HealthCheckResult
                        {
                            IsHealthy = false,
                            ServiceStatus = "Running",
                            Message = "HTTP-проверка не ответила за " + config.ResponseTimeoutSeconds + " с: " + config.HttpUrl
                        };
                    }

                    var response = task.Result;
                    var code = (int)response.StatusCode;
                    if (response.IsSuccessStatusCode)
                    {
                        return new HealthCheckResult
                        {
                            IsHealthy = true,
                            ServiceStatus = "Running",
                            Message = string.Format("Служба Running, HTTP {0} → {1}", config.HttpUrl, code)
                        };
                    }

                    return new HealthCheckResult
                    {
                        IsHealthy = false,
                        ServiceStatus = "Running",
                        Message = string.Format("HTTP {0} вернул {1} ({2})", config.HttpUrl, code, response.ReasonPhrase)
                    };
                }
            }
            catch (Exception ex)
            {
                var inner = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    ServiceStatus = "Running",
                    Message = "Ошибка HTTP-проверки " + config.HttpUrl + ": " + inner.Message
                };
            }
        }

        private static string StatusToString(ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Running: return "Running";
                case ServiceControllerStatus.Stopped: return "Stopped";
                case ServiceControllerStatus.StartPending: return "StartPending";
                case ServiceControllerStatus.StopPending: return "StopPending";
                case ServiceControllerStatus.Paused: return "Paused";
                case ServiceControllerStatus.PausePending: return "PausePending";
                case ServiceControllerStatus.ContinuePending: return "ContinuePending";
                default: return status.ToString();
            }
        }
    }
}

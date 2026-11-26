using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using WatchDogs.Core;

namespace WatchDogs.Service
{
    internal static class Program
    {
        private const int AttachParentProcess = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        /// <summary>
        /// Entry point.
        ///   (no args)              — run as Windows Service (SCM)
        ///   --console              — run monitoring loop in console (debug)
        ///   --install [--start]    — register Windows service
        ///   --uninstall            — stop and delete Windows service
        ///   --status               — print monitoring service status
        /// </summary>
        private static int Main(string[] args)
        {
            // Binary is WinExe (no console). Re-attach parent console for CLI verbs.
            if (args != null && args.Length > 0)
                AttachConsole(AttachParentProcess);

            if (args == null || args.Length == 0)
            {
                // Launched by Service Control Manager.
                ServiceBase.Run(new MonitorService());
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "--console":
                case "-c":
                    return RunConsole();

                case "--install":
                    return Install(args);

                case "--uninstall":
                case "--remove":
                    return Uninstall();

                case "--start":
                {
                    string msg;
                    var ok = ServiceInstallerHelper.Start(out msg);
                    Console.WriteLine(msg);
                    return ok ? 0 : 1;
                }

                case "--stop":
                {
                    string msg;
                    var ok = ServiceInstallerHelper.Stop(out msg);
                    Console.WriteLine(msg);
                    return ok ? 0 : 1;
                }

                case "--status":
                {
                    Console.WriteLine("Monitoring service: " + ServiceInstallerHelper.GetStatusText());
                    var status = ConfigStore.LoadStatus();
                    if (status != null)
                    {
                        Console.WriteLine("State: " + status.State);
                        Console.WriteLine("Target: " + status.TargetServiceName + " / " + status.TargetServiceStatus);
                        Console.WriteLine("Healthy: " + status.LastCheckHealthy);
                        Console.WriteLine("Message: " + status.LastCheckMessage);
                        Console.WriteLine("Checks: " + status.TotalChecks + ", Restarts: " + status.TotalRestarts);
                    }
                    else
                    {
                        Console.WriteLine("status.json not found yet.");
                    }
                    return 0;
                }

                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;

                default:
                    Console.Error.WriteLine("Unknown argument: " + command);
                    PrintHelp();
                    return 2;
            }
        }

        private static int RunConsole()
        {
            Console.WriteLine("Watch Dogs — консольный режим мониторинга (Ctrl+C для выхода)");
            var engine = new MonitorEngine();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                engine.RequestStop();
            };
            engine.Run();
            return 0;
        }

        private static int Install(string[] args)
        {
            string msg;
            if (!ServiceInstallerHelper.Install(out msg))
            {
                Console.Error.WriteLine(msg);
                return 1;
            }
            Console.WriteLine(msg);

            foreach (var a in args)
            {
                if (string.Equals(a, "--start", StringComparison.OrdinalIgnoreCase))
                {
                    string startMsg;
                    if (ServiceInstallerHelper.Start(out startMsg))
                        Console.WriteLine(startMsg);
                    else
                        Console.Error.WriteLine(startMsg);
                }
            }
            return 0;
        }

        private static int Uninstall()
        {
            string msg;
            if (!ServiceInstallerHelper.Uninstall(out msg))
            {
                Console.Error.WriteLine(msg);
                return 1;
            }
            Console.WriteLine(msg);
            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("WatchDogs.Service.exe [команда]");
            Console.WriteLine("  (нет аргументов)   — работа как Windows-служба");
            Console.WriteLine("  --console          — мониторинг в консоли (отладка)");
            Console.WriteLine("  --install [--start]— установить службу мониторинга");
            Console.WriteLine("  --uninstall        — удалить службу мониторинга");
            Console.WriteLine("  --start / --stop   — управлять службой мониторинга");
            Console.WriteLine("  --status           — показать текущее состояние");
        }
    }
}

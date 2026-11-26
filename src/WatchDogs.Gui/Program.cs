using System;
using System.Windows.Forms;
using WatchDogs.Core;

namespace WatchDogs.Gui
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Show real errors instead of a silent WinExe crash.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Fatal("Ошибка в интерфейсе", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Fatal("Критическая ошибка", e.ExceptionObject as Exception);

            try
            {
                Paths.EnsureDirectories();
            }
            catch (Exception ex)
            {
                Fatal("Не удалось создать папку данных %ProgramData%\\WatchDogs", ex);
                return;
            }

            try
            {
                Log.DebugEnabled = args != null && Array.IndexOf(args, "--debug") >= 0;
                Log.Info("GUI started");
            }
            catch
            {
                // logging must not block UI
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Fatal("Не удалось запустить Watch Dogs", ex);
            }
        }

        private static void Fatal(string title, Exception ex)
        {
            var detail = ex != null ? ex.ToString() : "(нет подробностей)";
            try { Log.Error(title, ex); } catch { /* ignore */ }

            try
            {
                MessageBox.Show(
                    title + ":\n\n" + detail +
                    "\n\nЖурнал: " + SafeLogPath(),
                    "Watch Dogs",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // last resort — nothing we can do
            }
        }

        private static string SafeLogPath()
        {
            try { return Log.GetLogFilePath(); }
            catch { return @"%ProgramData%\WatchDogs\logs"; }
        }
    }
}

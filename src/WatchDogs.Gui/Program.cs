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
            Paths.EnsureDirectories();
            Log.DebugEnabled = args != null && Array.IndexOf(args, "--debug") >= 0;
            Log.Info("GUI started");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

using System;
using System.Windows.Forms;
using GroceryPOS.Data;
using GroceryPOS.Services;
using GroceryPOS.UI;

namespace GroceryPOS
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
                MessageBox.Show(e.Exception.Message, "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Theme.InitScale();

#if DEBUG
            if (args.Length >= 2 && args[0] == "--snap") { SnapShots.Run(args[1]); return 0; }
#endif

            if (!ConnectDatabase()) return 1;

            while (true)
            {
                using (var login = new LoginForm(false))
                    if (login.ShowDialog() != DialogResult.OK) break;

                using (var main = new MainForm())
                {
                    Application.Run(main);
                    if (!main.SignOutRequested) break;
                }
            }
            return 0;
        }

        static bool ConnectDatabase()
        {
            while (true)
            {
                try
                {
                    Db.Initialize();
                    AppSettings.Load();
                    return true;
                }
                catch (Exception ex)
                {
                    var r = MessageBox.Show(
                        "Could not connect to SQL Server at \"" + Db.Server + "\".\n\n" + ex.Message +
                        "\n\nMake sure SQL Server Express is running (service MSSQL$SQLEXPRESS), or change the SqlServer value in GroceryPOS.exe.config.",
                        "Database connection", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                    if (r != DialogResult.Retry) return false;
                }
            }
        }
    }
}

// ============================================================
// FILE: Program.cs
// Production entry point:
//   1. Initialize database (create tables if first run)
//   2. Build services (manual dependency injection)
//   3. Show login screen
//   4. Only open main window on successful login
// ============================================================

using LeaveTrackerPro.Data;
using LeaveTrackerPro.Forms;
using LeaveTrackerPro.Services;

namespace LeaveTrackerPro
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // ---- Step 1: Initialize database ----
            // Creates the SQLite file and all tables on first run.
            // On subsequent runs, it checks for any new tables/columns.
            try
            {
                AppDbContext.InitializeDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize the database:\n\n{ex.Message}\n\nThe app cannot start.",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // ---- Step 2: Build services ----
            // We wire up dependencies manually (no DI container needed for WinForms).
            // Order matters: AuditService has no dependencies, others depend on it.
            var auditService = new AuditService();
            var emailService = new EmailService();
            var authService = new AuthService(auditService);
            var leaveService = new LeaveService(auditService, emailService);
            var userService = new UserService(auditService);
            var exportService = new ExportService();

            // ---- Step 3: Show login loop ----
            // Keep showing login until user successfully logs in or closes the window.
            while (true)
            {
                using var loginForm = new LoginForm(authService);
                var loginResult = loginForm.ShowDialog();

                if (loginResult != DialogResult.OK)
                    break; // User closed the login window — exit app

                // ---- Step 4: Show main window ----
                using var mainForm = new MainForm(leaveService, userService,
                                                   authService, exportService, auditService);
                Application.Run(mainForm);

                // After main form closes, check if it was a logout (restart loop)
                // or a window close (exit app)
                if (!Helpers.Session.IsLoggedIn)
                    continue; // Was a logout — show login again
                else
                    break; // Was a window close — exit
            }
        }
    }
}
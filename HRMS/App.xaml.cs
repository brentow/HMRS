using HRMS.Model;
using MySqlConnector;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HRMS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up global exception handling for UI thread
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Set up global exception handling for Task-based async operations
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Set up global exception handling for non-UI threads
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            StartMenuShortcutService.EnsureInstalledShortcutUsesHrmsIcon();
            base.OnStartup(e);

            Dispatcher.BeginInvoke(
                new Action(() => _ = VerifyOnlineDatabasesOnStartupAsync()),
                DispatcherPriority.ApplicationIdle);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowCriticalError(e.Exception, "UI Thread Error");
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            ShowCriticalError(e.Exception, "Async Task Error");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowCriticalError(ex, "Domain Error");
            }
        }

        private static void ShowCriticalError(Exception ex, string source)
        {
            MessageBox.Show(
                $"A critical {source} has occurred.\n\n" +
                $"Details: {ex.Message}\n\n" +
                "The application will attempt to remain stable. Please report this if it persists.",
                "HRMS System Stability",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private static async Task VerifyOnlineDatabasesOnStartupAsync()
        {
            try
            {
                await InitializeOnlineDatabasesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "HRMS could not verify the configured online database connection.\n\n" +
                    "The login page will still open so you can use Setup to review the saved Hostinger connection.\n\n" +
                    $"Details: {ex.Message}",
                    "Online Database Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static async Task InitializeOnlineDatabasesAsync()
        {
            await TestConnectionAsync(DbConfig.ConnectionString, "HRMS");
            await new DbMigrationService(DbConfig.ConnectionString).ApplyPendingMigrationsAsync();

            await TestConnectionAsync(GgmsConfig.ConnectionString, "GGMS");
            await TestConnectionAsync(CrsConfig.ConnectionString, "CRS");
        }

        private static async Task TestConnectionAsync(string connectionString, string label)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                ConnectionTimeout = 8,
                DefaultCommandTimeout = 20
            };

            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("SELECT 1;", connection);
            var result = await command.ExecuteScalarAsync();
            if (Convert.ToInt32(result) != 1)
            {
                throw new InvalidOperationException($"{label} database did not respond to a health check.");
            }
        }
    }
}

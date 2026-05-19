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
            StartMenuShortcutService.EnsureInstalledShortcutUsesHrmsIcon();
            base.OnStartup(e);

            Dispatcher.BeginInvoke(
                new Action(() => _ = VerifyOnlineDatabasesOnStartupAsync()),
                DispatcherPriority.ApplicationIdle);
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

using HRMS.Model;
using System.Windows;

namespace HRMS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            StartMenuShortcutService.EnsureInstalledShortcutUsesHrmsIcon();
            base.OnStartup(e);
        }
    }
}

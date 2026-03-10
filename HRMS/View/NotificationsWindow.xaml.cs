using HRMS.Model;
using HRMS.ViewModel;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class NotificationsWindow : UserControl
    {
        public NotificationsWindow()
        {
            InitializeComponent();
            var vm = new NotificationsViewModel();
            vm.OpenModuleRequested += OnOpenModuleRequested;
            DataContext = vm;
        }

        public event EventHandler<string>? OpenModuleRequested;

        public async Task RefreshAsync()
        {
            if (DataContext is NotificationsViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is NotificationsViewModel vm)
            {
                vm.SetCurrentUser(user);
            }
        }

        private void OnOpenModuleRequested(object? sender, string moduleKey)
        {
            OpenModuleRequested?.Invoke(this, moduleKey);
        }
    }
}

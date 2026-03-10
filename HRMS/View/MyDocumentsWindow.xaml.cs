using HRMS.Model;
using HRMS.ViewModel;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class MyDocumentsWindow : UserControl
    {
        public MyDocumentsWindow()
        {
            InitializeComponent();
            var vm = new MyDocumentsViewModel();
            vm.OpenModuleRequested += OnOpenModuleRequested;
            DataContext = vm;
        }

        public event EventHandler<string>? OpenModuleRequested;

        public async Task RefreshAsync()
        {
            if (DataContext is MyDocumentsViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is MyDocumentsViewModel vm)
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

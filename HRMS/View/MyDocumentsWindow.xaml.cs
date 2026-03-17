using HRMS.Model;
using HRMS.ViewModel;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class DocumentsWindow : UserControl
    {
        public DocumentsWindow()
        {
            InitializeComponent();
            var vm = new DocumentsViewModel();
            vm.OpenModuleRequested += OnOpenModuleRequested;
            DataContext = vm;
        }

        public event EventHandler<string>? OpenModuleRequested;

        public async Task RefreshAsync()
        {
            if (DataContext is DocumentsViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is DocumentsViewModel vm)
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

using HRMS.ViewModel;
using HRMS.Model;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class LeaveWindow : UserControl
    {
        public LeaveWindow()
        {
            InitializeComponent();
            DataContext = new LeaveViewModel();
        }

        public async Task RefreshAsync()
        {
            if (DataContext is LeaveViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is LeaveViewModel vm)
            {
                vm.SetCurrentUser(user?.UserId ?? 0, user?.Username ?? "-", user?.RoleName);
            }
        }
    }
}

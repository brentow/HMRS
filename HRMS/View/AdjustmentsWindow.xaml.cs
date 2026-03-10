using System.Threading.Tasks;
using System.Windows.Controls;
using HRMS.ViewModel;
using HRMS.Model;

namespace HRMS.View
{
    public partial class AdjustmentsWindow : UserControl
    {
        public AdjustmentsWindow()
        {
            InitializeComponent();
            DataContext = new AttendanceViewModel();
        }

        public async Task RefreshAsync()
        {
            if (DataContext is AttendanceViewModel vm)
            {
                await vm.RefreshAsync();
            }
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (DataContext is AttendanceViewModel vm)
            {
                vm.SetCurrentUser(user?.UserId ?? 0, user?.Username ?? "-", user?.RoleName);
            }
        }
    }
}

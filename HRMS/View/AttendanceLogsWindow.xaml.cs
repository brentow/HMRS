using System.Threading.Tasks;
using System.Windows.Controls;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class AttendanceLogsWindow : UserControl
    {
        public AttendanceLogsWindow()
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
    }
}

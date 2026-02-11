using System.Windows.Controls;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class AttendanceWindow : UserControl
    {
        public AttendanceWindow()
        {
            InitializeComponent();
            DataContext = new AttendanceViewModel();
        }
    }
}

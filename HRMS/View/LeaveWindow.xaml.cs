using System.Windows.Controls;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class LeaveWindow : UserControl
    {
        public LeaveWindow()
        {
            InitializeComponent();
            DataContext = new LeaveViewModel();
        }
    }
}

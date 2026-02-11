using System.Windows.Controls;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class EmployeesWindow : UserControl
    {
        public EmployeesWindow()
        {
            InitializeComponent();
            DataContext = new EmployeesViewModel();
        }
    }
}

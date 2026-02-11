using System.Windows.Controls;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class DepartmentsWindow : UserControl
    {
        public DepartmentsWindow()
        {
            InitializeComponent();
            DataContext = new DepartmentsViewModel();
        }
    }
}

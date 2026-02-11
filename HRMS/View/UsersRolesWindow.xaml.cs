using System.Windows.Controls;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class UsersRolesWindow : UserControl
    {
        public UsersRolesWindow()
        {
            InitializeComponent();
            DataContext = new UsersRolesViewModel();
        }
    }
}

using System.Threading.Tasks;
using System.Windows.Controls;
using HRMS.Model;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class PayrollWindow : UserControl
    {
        private readonly PayrollViewModel _viewModel;

        public PayrollWindow()
        {
            InitializeComponent();
            _viewModel = new PayrollViewModel();
            DataContext = _viewModel;
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            if (user == null)
            {
                _viewModel.SetCurrentUser(0, string.Empty, null);
                return;
            }

            _viewModel.SetCurrentUser(user.UserId, user.Username, user.RoleName);
        }

        public async Task RefreshAsync()
        {
            await _viewModel.RefreshAsync();
        }
    }
}

using HRMS.Model;
using HRMS.ViewModel;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class DocumentVerificationWindow : UserControl
    {
        private readonly DocumentVerificationViewModel _viewModel;

        public DocumentVerificationWindow()
        {
            InitializeComponent();
            _viewModel = new DocumentVerificationViewModel();
            DataContext = _viewModel;
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            _viewModel.SetCurrentUser(user);
        }

        public async Task RefreshAsync()
        {
            await _viewModel.RefreshAsync();
        }
    }
}

using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class RecruitmentWindow : UserControl
    {
        public RecruitmentWindow()
        {
            InitializeComponent();
        }

        public async Task RefreshAsync()
        {
            var vm = DataContext as RecruitmentViewModel;
            if (vm == null && Content is FrameworkElement root)
            {
                vm = root.DataContext as RecruitmentViewModel;
            }

            if (vm != null)
            {
                await vm.RefreshAsync();
            }
        }
    }
}

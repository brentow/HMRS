using HRMS.ViewModel;
using System.Windows.Controls;

namespace HRMS.View
{
    /// <summary>
    /// Beneficiary Verification window for admin to review and approve/reject staged beneficiaries.
    /// </summary>
    public partial class BeneficiaryVerificationWindow : UserControl
    {
        public BeneficiaryVerificationWindow()
        {
            InitializeComponent();
            DataContext = new BeneficiaryVerificationViewModel();
        }
    }
}

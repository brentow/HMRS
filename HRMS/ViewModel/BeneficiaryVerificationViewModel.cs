using HRMS.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class BeneficiaryVerificationViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));
        private static readonly Brush WarningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9B53B"));

        private readonly BeneficiaryDataService _dataService = new(DbConfig.ConnectionString);

        private bool _isBusy;
        private string _selectedStatusFilter = "Pending";
        private string _actionMessage = "Ready";
        private Brush _actionMessageBrush = InfoBrush;
        private int _pendingCount;
        private int _approvedCount;
        private int _rejectedCount;

        private BeneficiaryStagingDto? _selectedBeneficiary;
        private string _remarksInput = string.Empty;

        public ObservableCollection<BeneficiaryStagingDto> Beneficiaries { get; } = new();
        public ObservableCollection<string> StatusFilters { get; } = new() { "Pending", "Approved", "Rejected", "All" };

        public ICommand RefreshCommand { get; }
        public ICommand ApproveBeneficiaryCommand { get; }
        public ICommand RejectBeneficiaryCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        public string ActionMessage
        {
            get => _actionMessage;
            private set
            {
                if (_actionMessage == value) return;
                _actionMessage = value;
                OnPropertyChanged();
            }
        }

        public Brush ActionMessageBrush
        {
            get => _actionMessageBrush;
            private set
            {
                if (_actionMessageBrush == value) return;
                _actionMessageBrush = value;
                OnPropertyChanged();
            }
        }

        public int PendingCount { get => _pendingCount; private set { _pendingCount = value; OnPropertyChanged(); } }
        public int ApprovedCount { get => _approvedCount; private set { _approvedCount = value; OnPropertyChanged(); } }
        public int RejectedCount { get => _rejectedCount; private set { _rejectedCount = value; OnPropertyChanged(); } }

        public BeneficiaryStagingDto? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set
            {
                if (_selectedBeneficiary == value) return;
                _selectedBeneficiary = value;
                OnPropertyChanged();
                RemarksInput = string.Empty;
            }
        }

        public string RemarksInput
        {
            get => _remarksInput;
            set
            {
                if (_remarksInput == value) return;
                _remarksInput = value;
                OnPropertyChanged();
            }
        }

        public BeneficiaryVerificationViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
            ApproveBeneficiaryCommand = new AsyncRelayCommand(_ => ApproveBeneficiaryAsync());
            RejectBeneficiaryCommand = new AsyncRelayCommand(_ => RejectBeneficiaryAsync());

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Loading beneficiary staging data...", InfoBrush);

            try
            {
                // Load status counts
                var counts = await _dataService.GetStatusCountsAsync();
                PendingCount = counts.Pending;
                ApprovedCount = counts.Approved;
                RejectedCount = counts.Rejected;

                // Determine which status filter to apply
                BeneficiaryVerificationStatus? statusFilter = null;
                if (SelectedStatusFilter == "Pending")
                    statusFilter = BeneficiaryVerificationStatus.Pending;
                else if (SelectedStatusFilter == "Approved")
                    statusFilter = BeneficiaryVerificationStatus.Approved;
                else if (SelectedStatusFilter == "Rejected")
                    statusFilter = BeneficiaryVerificationStatus.Rejected;
                // null if "All" is selected

                var beneficiaries = await _dataService.GetStagingBeneficiariesAsync(statusFilter, limit: 1000);

                Beneficiaries.Clear();
                foreach (var benef in beneficiaries)
                {
                    Beneficiaries.Add(benef);
                }

                SetMessage($"Loaded {beneficiaries.Count} beneficiaries", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Error loading beneficiaries: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApproveBeneficiaryAsync()
        {
            if (SelectedBeneficiary == null)
            {
                SetMessage("Please select a beneficiary first.", WarningBrush);
                return;
            }

            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Approving beneficiary and creating account...", InfoBrush);

            try
            {
                // Step 1: Update verification status
                await _dataService.UpdateVerificationStatusAsync(
                    SelectedBeneficiary.StagingID,
                    BeneficiaryVerificationStatus.Approved,
                    RemarksInput);

                // Step 2: Create user account
                string temporaryPassword;
                try
                {
                    var (userId, tempPassword) = await _dataService.CreateBeneficiaryAccountAsync(
                        SelectedBeneficiary.StagingID,
                        SelectedBeneficiary);
                    temporaryPassword = tempPassword;
                }
                catch (Exception accountEx)
                {
                    // If account creation fails, revert the status update
                    await _dataService.UpdateVerificationStatusAsync(
                        SelectedBeneficiary.StagingID,
                        BeneficiaryVerificationStatus.Pending,
                        "Account creation failed - status reverted");

                    SetMessage($"Approval failed: Account creation error - {accountEx.Message}", ErrorBrush);
                    return;
                }

                // Success: show password and notification
                SetMessage(
                    $"✓ Approved {SelectedBeneficiary.FirstName} {SelectedBeneficiary.LastName}\n" +
                    $"✓ Account created. Temporary password: {temporaryPassword}\n" +
                    $"(Password expires on first login)",
                    SuccessBrush);

                SelectedBeneficiary = null;
                RemarksInput = string.Empty;
                await LoadAsync();

                SystemRefreshBus.Raise("BeneficiaryVerificationUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Error approving beneficiary: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RejectBeneficiaryAsync()
        {
            if (SelectedBeneficiary == null)
            {
                SetMessage("Please select a beneficiary first.", WarningBrush);
                return;
            }

            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Rejecting beneficiary...", InfoBrush);

            try
            {
                await _dataService.UpdateVerificationStatusAsync(
                    SelectedBeneficiary.StagingID,
                    BeneficiaryVerificationStatus.Rejected,
                    RemarksInput);

                SetMessage($"Successfully rejected {SelectedBeneficiary.FirstName} {SelectedBeneficiary.LastName}", SuccessBrush);
                SelectedBeneficiary = null;
                RemarksInput = string.Empty;
                await LoadAsync();

                SystemRefreshBus.Raise("BeneficiaryVerificationUpdated");
            }
            catch (Exception ex)
            {
                SetMessage($"Error rejecting beneficiary: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

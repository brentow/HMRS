using HRMS.Model;
using HRMS.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class BiometricAttendanceWindow : Window
    {
        private readonly BiometricAttendanceKioskViewModel _viewModel;

        public BiometricAttendanceWindow(AuthenticatedUser? user)
        {
            InitializeComponent();

            _viewModel = new BiometricAttendanceKioskViewModel(user);
            DataContext = _viewModel;

            Loaded += BiometricAttendanceWindow_OnLoaded;
            Closed += BiometricAttendanceWindow_OnClosed;
        }

        private async void BiometricAttendanceWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void BiometricAttendanceWindow_OnClosed(object? sender, System.EventArgs e)
        {
            _viewModel.Dispose();
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PunchButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string promptTitle)
            {
                FingerprintPromptTitle.Text = promptTitle;
            }

            FingerprintPromptOverlay.Visibility = Visibility.Visible;
        }

        private void CloseFingerprintPrompt_OnClick(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelActivePunch();
            FingerprintPromptOverlay.Visibility = Visibility.Collapsed;
        }
    }
}

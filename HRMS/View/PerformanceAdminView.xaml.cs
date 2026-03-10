using HRMS.ViewModel;
using HRMS.Model;
using System;
using System.Windows;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class PerformanceAdminView : UserControl
    {
        public event EventHandler? CloseRequested;

        public PerformanceAdminView()
        {
            InitializeComponent();
        }

        private PerformanceViewModel? Vm => DataContext as PerformanceViewModel;

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (Vm is null)
            {
                return;
            }

            try
            {
                await Vm.RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to refresh performance data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveCycle_Click(object sender, RoutedEventArgs e)
        {
            if (Vm is null || (sender as FrameworkElement)?.DataContext is not PerformanceCycleRowVm cycle)
            {
                return;
            }

            try
            {
                await Vm.SaveCycleAsync(cycle);
                SystemRefreshBus.Raise("PerformanceCycleUpdated");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save cycle: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveReview_Click(object sender, RoutedEventArgs e)
        {
            if (Vm is null || (sender as FrameworkElement)?.DataContext is not PerformanceReviewRowVm review)
            {
                return;
            }

            try
            {
                await Vm.SaveReviewAsync(review);
                SystemRefreshBus.Raise("PerformanceReviewUpdated");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save review: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

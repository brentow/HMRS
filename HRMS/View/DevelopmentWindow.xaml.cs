using HRMS.Model;
using HRMS.ViewModel;
using ScottPlot;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class DevelopmentWindow : UserControl, INotifyPropertyChanged
    {
        private bool _isEmployeeMode;

        public TrainingViewModel TrainingVm { get; } = new();
        public PerformanceViewModel PerformanceVm { get; } = new();

        public bool IsEmployeeMode
        {
            get => _isEmployeeMode;
            private set
            {
                if (_isEmployeeMode == value)
                {
                    return;
                }

                _isEmployeeMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAdminOrHrMode));
                OnPropertyChanged(nameof(TrainingHeaderTitle));
                OnPropertyChanged(nameof(TrainingHeaderSubtitle));
                OnPropertyChanged(nameof(PerformanceHeaderTitle));
                OnPropertyChanged(nameof(PerformanceHeaderSubtitle));
            }
        }

        public bool IsAdminOrHrMode => !IsEmployeeMode;

        public string TrainingHeaderTitle => IsEmployeeMode ? "My Training" : "Training";

        public string TrainingHeaderSubtitle => IsEmployeeMode
            ? "View your enrolled courses and completion status."
            : "Courses, sessions, and enrollments";

        public string PerformanceHeaderTitle => IsEmployeeMode ? "My Performance" : "Performance";

        public string PerformanceHeaderSubtitle => IsEmployeeMode
            ? "Track your cycles, reviews, and current rating."
            : "Cycles, goals, reviews, and review items";

        public DevelopmentWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += DevelopmentWindow_Loaded;
            Unloaded += DevelopmentWindow_Unloaded;
            PerformanceVm.PropertyChanged += PerformanceVm_PropertyChanged;
            UpdateRoleScopedUi();
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            var roleName = user?.RoleName;
            IsEmployeeMode = string.Equals(roleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);

            TrainingVm.SetCurrentUser(user?.UserId ?? 0, roleName);
            PerformanceVm.SetCurrentUser(user?.UserId ?? 0, roleName);
            UpdateRoleScopedUi();
        }

        public async Task RefreshAsync()
        {
            await TrainingVm.RefreshAsync();
            await PerformanceVm.RefreshAsync();
            RenderReviewStatusChart();
        }

        private async void DevelopmentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshAsync();
            }
            catch
            {
                // Keep UI responsive even if DB is temporarily unavailable.
            }
        }

        private void DevelopmentWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            PerformanceVm.PropertyChanged -= PerformanceVm_PropertyChanged;
        }

        private void PerformanceVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PerformanceViewModel.SubmittedReviews) ||
                e.PropertyName == nameof(PerformanceViewModel.DraftReviews) ||
                e.PropertyName == nameof(PerformanceViewModel.TotalReviews))
            {
                RenderReviewStatusChart();
            }
        }

        private void UpdateRoleScopedUi()
        {
            var adminVisibility = IsAdminOrHrMode ? Visibility.Visible : Visibility.Collapsed;
            var employeeVisibility = IsEmployeeMode ? Visibility.Visible : Visibility.Collapsed;

            if (AddCourseButton != null)
            {
                AddCourseButton.Visibility = adminVisibility;
            }

            if (EnrollEmployeeButton != null)
            {
                EnrollEmployeeButton.Visibility = adminVisibility;
            }

            if (CourseActionColumn != null)
            {
                CourseActionColumn.Visibility = adminVisibility;
            }

            if (CourseRequestColumn != null)
            {
                CourseRequestColumn.Visibility = employeeVisibility;
            }

            if (EnrollmentStatusEditColumn != null)
            {
                EnrollmentStatusEditColumn.Visibility = adminVisibility;
            }

            if (EnrollmentStatusReadOnlyColumn != null)
            {
                EnrollmentStatusReadOnlyColumn.Visibility = employeeVisibility;
            }

            if (OpenPerformanceAdminButton != null)
            {
                OpenPerformanceAdminButton.Visibility = adminVisibility;
            }

            if (!IsAdminOrHrMode && PerformanceAdminHost != null)
            {
                PerformanceAdminHost.Visibility = Visibility.Collapsed;
            }
        }

        private void RenderReviewStatusChart()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RenderReviewStatusChart);
                return;
            }

            if (ReviewStatusPlot is null)
            {
                return;
            }

            var submitted = PerformanceVm.SubmittedReviews;
            var draft = PerformanceVm.DraftReviews;
            var remaining = Math.Max(0, PerformanceVm.TotalReviews - submitted - draft);

            var plot = ReviewStatusPlot.Plot;
            plot.Clear();

            var submittedBar = plot.AddBar(
                new[] { (double)submitted },
                new[] { 0.0 },
                ColorTranslator.FromHtml("#2563EB"));
            submittedBar.BarWidth = 0.5;
            submittedBar.BorderLineWidth = 0;
            submittedBar.ShowValuesAboveBars = true;
            submittedBar.ValueFormatter = value => value <= 0 ? string.Empty : value.ToString("0");
            submittedBar.Label = "Submitted";

            var draftBar = plot.AddBar(
                new[] { (double)draft },
                new[] { 1.0 },
                ColorTranslator.FromHtml("#F59E0B"));
            draftBar.BarWidth = 0.5;
            draftBar.BorderLineWidth = 0;
            draftBar.ShowValuesAboveBars = true;
            draftBar.ValueFormatter = value => value <= 0 ? string.Empty : value.ToString("0");
            draftBar.Label = "Draft";

            var remainingBar = plot.AddBar(
                new[] { (double)remaining },
                new[] { 2.0 },
                ColorTranslator.FromHtml("#10B981"));
            remainingBar.BarWidth = 0.5;
            remainingBar.BorderLineWidth = 0;
            remainingBar.ShowValuesAboveBars = true;
            remainingBar.ValueFormatter = value => value <= 0 ? string.Empty : value.ToString("0");
            remainingBar.Label = "Remaining";

            plot.XTicks(
            [
                0,
                1,
                2
            ],
            [
                "Submitted",
                "Draft",
                "Remaining"
            ]);

            var highest = Math.Max(submitted, Math.Max(draft, remaining));
            var yMax = Math.Max(1, highest + 1);

            plot.SetAxisLimits(yMin: 0, yMax: yMax);
            plot.SetAxisLimitsX(-0.5, 2.5);
            var yTickPositions = Enumerable.Range(1, yMax).Select(i => (double)i).ToArray();
            var yTickLabels = yTickPositions.Select(v => v.ToString("0")).ToArray();
            plot.YTicks(yTickPositions, yTickLabels);
            plot.Grid(lineStyle: LineStyle.Solid, color: ColorTranslator.FromHtml("#DCE8F8"));
            plot.XAxis.Grid(false);
            plot.YAxis.Grid(true);
            var frameColor = ColorTranslator.FromHtml("#111827");
            plot.XAxis.TickLabelStyle(color: frameColor, fontSize: 12);
            plot.YAxis.TickLabelStyle(color: frameColor, fontSize: 12);
            plot.XAxis.Color(frameColor);
            plot.YAxis.Color(frameColor);
            plot.XAxis2.Color(frameColor);
            plot.YAxis2.Color(frameColor);
            plot.XAxis2.Ticks(false);
            plot.YAxis2.Ticks(false);
            plot.Layout(left: 48, right: 8, top: 8, bottom: 40);

            ReviewStatusPlot.Refresh();
        }

        private void AddCourse_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdminOrHrMode)
            {
                return;
            }

            var dlg = new AddCourseWindow
            {
                Owner = Window.GetWindow(this),
                TrainingVm = TrainingVm
            };
            dlg.ShowDialog();
        }

        private void EnrollEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdminOrHrMode)
            {
                return;
            }

            var dlg = new EnrollEmployeeWindow
            {
                Owner = Window.GetWindow(this),
                TrainingVm = TrainingVm
            };
            dlg.ShowDialog();
        }

        private void EditCourse_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdminOrHrMode)
            {
                return;
            }

            if ((sender as FrameworkElement)?.DataContext is not TrainingCourseSummary course)
            {
                return;
            }

            var dlg = new CourseEditWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = course,
                TrainingVm = TrainingVm
            };
            dlg.ShowDialog();
        }

        private async void CourseActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsAdminOrHrMode)
            {
                return;
            }

            if (sender is not ComboBox combo ||
                combo.DataContext is not TrainingCourseSummary course ||
                combo.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Content is not string selectedAction)
            {
                return;
            }

            // Ignore template/material initialization changes and only react to user pick.
            if (!combo.IsDropDownOpen)
            {
                return;
            }

            var previousStatus = course.Status;
            if (string.Equals(selectedAction, previousStatus, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var service = new TrainingDataService(DbConfig.ConnectionString);

                if (string.Equals(selectedAction, "Delete", StringComparison.OrdinalIgnoreCase))
                {
                    var deleteResult = MessageBox.Show(
                        $"Delete course '{course.Title}'?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (deleteResult != MessageBoxResult.Yes)
                    {
                        combo.SelectedValue = previousStatus;
                        return;
                    }

                    await service.DeleteCourseAsync(course.Id);
                    TrainingVm.RemoveCourse(course.Id);
                    SystemRefreshBus.Raise("TrainingCourseDeleted");
                    return;
                }

                var dto = new TrainingCourseDto(
                    course.Id,
                    course.Title,
                    course.Provider,
                    course.Description,
                    course.Hours,
                    selectedAction);

                await service.UpdateCourseAsync(dto);

                course.Status = selectedAction;
                course.StatusColor = TrainingViewModel.GetStatusBrush(selectedAction);
                TrainingVm.RefreshFilter();
                SystemRefreshBus.Raise("TrainingCourseUpdated");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to update course: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                combo.SelectedValue = previousStatus;
            }
        }

        private async void EnrollmentStatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsAdminOrHrMode)
            {
                return;
            }

            if (sender is not ComboBox combo ||
                combo.DataContext is not TrainingEnrollment enrollment ||
                combo.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Content is not string selectedStatus)
            {
                return;
            }

            // Ignore template/material initialization changes and only react to user pick.
            if (!combo.IsDropDownOpen)
            {
                return;
            }

            if (string.Equals(selectedStatus, enrollment.Status, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                await TrainingVm.ApplyEnrollmentActionAsync(enrollment, selectedStatus);

                if (!string.Equals(selectedStatus, "Delete", StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedValue = enrollment.Status;
                }

                SystemRefreshBus.Raise("TrainingEnrollmentUpdated");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to update enrollment status: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                combo.SelectedValue = enrollment.Status;
            }
        }

        private async void RequestCourse_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEmployeeMode)
            {
                return;
            }

            if ((sender as FrameworkElement)?.DataContext is not TrainingCourseSummary course)
            {
                return;
            }

            try
            {
                var result = await TrainingVm.RequestEnrollmentAsync(course);
                MessageBox.Show(
                    result.Message,
                    result.Success ? "Request Submitted" : "Enrollment Request",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

                if (result.Success)
                {
                    SystemRefreshBus.Raise("TrainingEnrollmentRequested");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to submit enrollment request: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OpenPerformanceAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdminOrHrMode)
            {
                return;
            }

            PerformanceAdminHost.Visibility = Visibility.Visible;
            try
            {
                await PerformanceVm.RefreshAsync();
                RenderReviewStatusChart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to refresh performance data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PerformanceAdminPanel_CloseRequested(object? sender, EventArgs e)
        {
            PerformanceAdminHost.Visibility = Visibility.Collapsed;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

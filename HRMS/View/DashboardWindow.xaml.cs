using HRMS.Model;
using HRMS.ViewModel;
using ScottPlot;
using System;
using System.ComponentModel;
using ColorTranslator = System.Drawing.ColorTranslator;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HRMS.View;

namespace HRMS.View
{
    public partial class DashboardWindow : Window
    {
        private bool _navCollapsed = false;
        private readonly GridLength _expandedWidth = new GridLength(270);
        private readonly GridLength _collapsedWidth = new GridLength(70);
        private readonly Thickness _expandedNavMargin = new Thickness(18, 22, 18, 0);
        private readonly Thickness _collapsedNavMargin = new Thickness(18, 130, 18, 0);
        private readonly DashboardDataService _dashboardDataService = new(DbConfig.ConnectionString);
        private readonly AuthenticatedUser? _authenticatedUser;
        private readonly NotificationsViewModel _notificationsViewModel = new();
        private bool _isRenderingDashboardCharts;
        private bool _isRefreshingAllModules;

        private enum ModuleKey
        {
            Dashboard,
            Employees,
            Departments,
            Attendance,
            AttendanceLogs,
            Adjustments,
            Leave,
            Payroll,
            MyDocuments,
            Recruitment,
            Development,
            Users
        }

        private bool IsAdminAccess =>
            string.Equals(_authenticatedUser?.RoleName?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

        private bool IsHrAccess =>
            string.Equals(_authenticatedUser?.RoleName?.Trim(), "HR Manager", StringComparison.OrdinalIgnoreCase);

        private bool IsEmployeeAccess =>
            string.Equals(_authenticatedUser?.RoleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);

        public DashboardWindow() : this(null)
        {
        }

        public DashboardWindow(AuthenticatedUser? authenticatedUser)
        {
            InitializeComponent();
            _authenticatedUser = authenticatedUser;
            UsersRolesModule?.SetCurrentUser(_authenticatedUser);
            AttendanceModule?.SetCurrentUser(_authenticatedUser);
            AdjustmentsModule?.SetCurrentUser(_authenticatedUser);
            LeaveModule?.SetCurrentUser(_authenticatedUser);
            PayrollModule?.SetCurrentUser(_authenticatedUser);
            DevelopmentModule?.SetCurrentUser(_authenticatedUser);
            MyDocumentsModule?.SetCurrentUser(_authenticatedUser);
            if (MyDocumentsModule != null)
            {
                MyDocumentsModule.OpenModuleRequested += EmployeeModule_OpenModuleRequested;
            }
            _notificationsViewModel.SetCurrentUser(_authenticatedUser);
            _notificationsViewModel.OpenModuleRequested += EmployeeModule_OpenModuleRequested;
            var vm = new DashboardViewModel(_authenticatedUser);
            DataContext = vm;
            DashboardNotificationsPopup.DataContext = _notificationsViewModel;
            vm.PropertyChanged += DashboardViewModel_PropertyChanged;
            Loaded += DashboardWindow_Loaded;
            Unloaded += DashboardWindow_Unloaded;
            ApplyCurrentUserCard();
            ApplyRoleBasedNavigation();
        }

        private async void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SystemRefreshBus.DataChanged += SystemRefreshBus_DataChanged;
            await RenderDashboardChartsAsync();
        }

        private void DashboardWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemRefreshBus.DataChanged -= SystemRefreshBus_DataChanged;

            if (DataContext is DashboardViewModel vm)
            {
                vm.PropertyChanged -= DashboardViewModel_PropertyChanged;
            }

            if (MyDocumentsModule != null)
            {
                MyDocumentsModule.OpenModuleRequested -= EmployeeModule_OpenModuleRequested;
            }

            _notificationsViewModel.OpenModuleRequested -= EmployeeModule_OpenModuleRequested;
        }

        private void SystemRefreshBus_DataChanged(object? sender, SystemDataChangedEventArgs e)
        {
            _ = Dispatcher.InvokeAsync(RefreshWholeSystemAsync);
        }

        private async Task RefreshWholeSystemAsync()
        {
            if (_isRefreshingAllModules)
            {
                return;
            }

            _isRefreshingAllModules = true;
            try
            {
                if (DataContext is DashboardViewModel vm)
                {
                    await vm.RefreshAsync();
                }

                await RenderDashboardChartsAsync();

                if (EmployeesModule != null)
                {
                    await EmployeesModule.RefreshAsync();
                }

                if (DepartmentsModule != null)
                {
                    await DepartmentsModule.RefreshAsync();
                }

                if (AttendanceModule != null)
                {
                    await AttendanceModule.RefreshAsync();
                }

                if (AttendanceLogsModule != null)
                {
                    await AttendanceLogsModule.RefreshAsync();
                }

                if (AdjustmentsModule != null)
                {
                    await AdjustmentsModule.RefreshAsync();
                }

                if (LeaveModule != null)
                {
                    await LeaveModule.RefreshAsync();
                }

                if (PayrollModule != null)
                {
                    await PayrollModule.RefreshAsync();
                }

                if (MyDocumentsModule != null)
                {
                    await MyDocumentsModule.RefreshAsync();
                }

                if (RecruitmentModule != null)
                {
                    await RecruitmentModule.RefreshAsync();
                }

                if (DevelopmentModule != null)
                {
                    await DevelopmentModule.RefreshAsync();
                }

                if (UsersRolesModule != null)
                {
                    await UsersRolesModule.RefreshAsync();
                }

                await _notificationsViewModel.RefreshAsync();
            }
            finally
            {
                _isRefreshingAllModules = false;
            }
        }

        private void DashboardViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DashboardViewModel vm)
            {
                return;
            }

            if ((e.PropertyName == nameof(DashboardViewModel.LastUpdated) && vm.IsDashboardVisible) ||
                (e.PropertyName == nameof(DashboardViewModel.IsDashboardVisible) && vm.IsDashboardVisible))
            {
                _ = RenderDashboardChartsAsync();
            }
        }

        private async Task RenderDashboardChartsAsync()
        {
            if (!IsLoaded ||
                _isRenderingDashboardCharts ||
                AttendanceCoveragePlot is null)
            {
                return;
            }

            _isRenderingDashboardCharts = true;
            try
            {
                var coverage = await _dashboardDataService.GetAttendanceCoverageAsync(14);
                RenderAttendanceCoverageChart(coverage);
            }
            catch
            {
                // Keep dashboard responsive even when chart data cannot be read.
            }
            finally
            {
                _isRenderingDashboardCharts = false;
            }
        }

        private void RenderAttendanceCoverageChart(DashboardAttendanceCoverageData data)
        {
            var plot = AttendanceCoveragePlot.Plot;
            plot.Clear();

            var x = Enumerable.Range(0, data.Labels.Length).Select(i => (double)i).ToArray();

            var presentBars = plot.AddBar(data.PresentCounts, x.Select(v => v - 0.26).ToArray(), ColorTranslator.FromHtml("#2E9D5B"));
            presentBars.Label = "Present";
            presentBars.BarWidth = 0.24;
            presentBars.BorderLineWidth = 0;

            var leaveBars = plot.AddBar(data.OnLeaveCounts, x, ColorTranslator.FromHtml("#FDBD55"));
            leaveBars.Label = "On Leave";
            leaveBars.BarWidth = 0.24;
            leaveBars.BorderLineWidth = 0;

            var missingBars = plot.AddBar(data.MissingCounts, x.Select(v => v + 0.26).ToArray(), ColorTranslator.FromHtml("#D54B4B"));
            missingBars.Label = "Missing";
            missingBars.BarWidth = 0.24;
            missingBars.BorderLineWidth = 0;

            var yMax = Math.Max(
                data.PresentCounts.Length == 0 ? 0 : data.PresentCounts.Max(),
                Math.Max(
                    data.OnLeaveCounts.Length == 0 ? 0 : data.OnLeaveCounts.Max(),
                    data.MissingCounts.Length == 0 ? 0 : data.MissingCounts.Max()));

            plot.XTicks(x, data.Labels);
            plot.SetAxisLimits(yMin: 0, yMax: Math.Max(2, yMax + 1));
            ApplyCartesianStyle(plot);
            plot.Legend(location: Alignment.UpperLeft);
            plot.Layout(left: 46, right: 10, top: 8, bottom: 50);

            AttendanceCoveragePlot.Refresh();
        }

        private static void ApplyCartesianStyle(Plot plot)
        {
            var gridColor = ColorTranslator.FromHtml("#DDE8F6");
            var axisColor = ColorTranslator.FromHtml("#4A5B6C");

            plot.Grid(lineStyle: LineStyle.Solid, color: gridColor);
            plot.XAxis.Grid(false);
            plot.YAxis.Grid(true);
            plot.XAxis.TickLabelStyle(color: axisColor, fontSize: 11);
            plot.YAxis.TickLabelStyle(color: axisColor, fontSize: 11);
            plot.XAxis.Color(axisColor);
            plot.YAxis.Color(axisColor);
            plot.XAxis2.Color(axisColor);
            plot.YAxis2.Color(axisColor);
            plot.XAxis2.Ticks(false);
            plot.YAxis2.Ticks(false);
        }

        private void ApplyCurrentUserCard()
        {
            if (_authenticatedUser == null)
            {
                CurrentUserNameText.Text = "Current User";
                CurrentUserRoleText.Text = "Guest";
                CurrentUserStatusText.Text = "Inactive";
                CurrentUserStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9AAFC8"));
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(_authenticatedUser.FullName)
                ? _authenticatedUser.Username
                : _authenticatedUser.FullName;
            var isOnline = string.Equals(_authenticatedUser.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            var role = string.IsNullOrWhiteSpace(_authenticatedUser.RoleName) ? "User" : _authenticatedUser.RoleName;

            CurrentUserNameText.Text = displayName;
            CurrentUserRoleText.Text = role;
            CurrentUserStatusText.Text = isOnline ? "Active" : "Inactive";
            CurrentUserStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isOnline ? "#3BC46B" : "#9AAFC8"));
            UserAccountSettingsButton.ToolTip = IsAdminAccess ? "Account Information" : "My Profile";
        }

        private void ApplyRoleBasedNavigation()
        {
            DashboardNavButton.Visibility = Visibility.Visible;
            EmployeesNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Employees));
            DepartmentsNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Departments));
            AttendanceNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Attendance));
            AttendanceLogsNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.AttendanceLogs));
            AdjustmentsNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Adjustments));
            LeaveNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Leave));
            PayrollNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Payroll));
            MyDocumentsNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.MyDocuments));
            RecruitmentNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Recruitment));
            DevelopmentNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Development));

            if (QuickEmployeesButton != null)
            {
                QuickEmployeesButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Employees));
            }

            if (QuickAttendanceButton != null)
            {
                QuickAttendanceButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Attendance));
            }

            if (QuickLeaveButton != null)
            {
                QuickLeaveButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Leave));
            }

            if (QuickPayrollButton != null)
            {
                QuickPayrollButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Payroll));
            }

            if (QuickDevelopmentButton != null)
            {
                QuickDevelopmentButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Development));
            }

            if (QuickDocumentsButton != null)
            {
                QuickDocumentsButton.Visibility = ToVisibility(CanOpenPersonalDocuments());
            }

            if (QuickActionsGrid != null)
            {
                var quickVisibleCount = 1; // Refresh is always visible
                if (QuickEmployeesButton?.Visibility == Visibility.Visible) quickVisibleCount++;
                if (QuickAttendanceButton?.Visibility == Visibility.Visible) quickVisibleCount++;
                if (QuickLeaveButton?.Visibility == Visibility.Visible) quickVisibleCount++;
                if (QuickPayrollButton?.Visibility == Visibility.Visible) quickVisibleCount++;
                if (QuickDevelopmentButton?.Visibility == Visibility.Visible) quickVisibleCount++;
                if (QuickDocumentsButton?.Visibility == Visibility.Visible) quickVisibleCount++;
                QuickActionsGrid.Columns = Math.Max(1, quickVisibleCount);
            }
        }

        private bool CanAccessModule(ModuleKey module)
        {
            if (IsAdminAccess)
            {
                return module switch
                {
                    ModuleKey.MyDocuments => false, // employee-only module
                    _ => true
                };
            }

            if (IsHrAccess)
            {
                return module switch
                {
                    ModuleKey.Dashboard => true,
                    ModuleKey.Employees => true,
                    ModuleKey.Departments => true,
                    ModuleKey.Attendance => true,
                    ModuleKey.AttendanceLogs => true,
                    ModuleKey.Adjustments => true,
                    ModuleKey.Leave => true,
                    ModuleKey.Payroll => true,
                    ModuleKey.MyDocuments => false,
                    ModuleKey.Recruitment => true,
                    ModuleKey.Development => true,
                    ModuleKey.Users => true, // profile tab only is enforced in UsersRolesWindow
                    _ => false
                };
            }

            if (IsEmployeeAccess)
            {
                return module switch
                {
                    ModuleKey.Dashboard => true,
                    ModuleKey.Attendance => true,
                    ModuleKey.Adjustments => true,
                    ModuleKey.Leave => true,
                    ModuleKey.Payroll => true,
                    ModuleKey.MyDocuments => true,
                    ModuleKey.Development => true,
                    ModuleKey.Users => true, // profile tab only
                    _ => false
                };
            }

            // Guests/default: dashboard + profile only
            return module is ModuleKey.Dashboard or ModuleKey.Users;
        }

        private bool CanOpenPersonalDocuments() =>
            _authenticatedUser?.EmployeeId is int employeeId && employeeId > 0;

        private bool EnsureModuleAccess(ModuleKey module, string moduleLabel)
        {
            if (CanAccessModule(module))
            {
                return true;
            }

            MessageBox.Show(
                $"Access denied. Your role cannot open {moduleLabel}.",
                "Access Control",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        private static Visibility ToVisibility(bool isVisible) =>
            isVisible ? Visibility.Visible : Visibility.Collapsed;

        public Rect GetMainContentBoundsOnScreen()
        {
            if (!IsLoaded || MainContentHost == null)
            {
                return Rect.Empty;
            }

            var topLeftInWindow = MainContentHost.TranslatePoint(new Point(0, 0), this);
            return new Rect(
                Left + topLeftInWindow.X,
                Top + topLeftInWindow.Y,
                MainContentHost.ActualWidth,
                MainContentHost.ActualHeight);
        }

        private void ToggleNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            _navCollapsed = !_navCollapsed;
            NavColumn.Width = _navCollapsed ? _collapsedWidth : _expandedWidth;

            UpdateToggleButtons();
            SetNavLabelsVisibility(_navCollapsed ? Visibility.Collapsed : Visibility.Visible);

            // Hide header and account card when collapsed
            NavHeaderPanel.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
            UserAccountCard.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;

            // Adjust icon stack margin for balanced spacing when collapsed
            NavStack.Margin = _navCollapsed ? _collapsedNavMargin : _expandedNavMargin;
        }

        private void SetNavLabelsVisibility(Visibility visibility)
        {
            SetLabelVisibilityInPanel(ModulesLabel, visibility);

            foreach (var textBlock in FindVisualChildren<TextBlock>(this))
            {
                if (textBlock.Tag as string == "NavLabel")
                {
                    textBlock.Visibility = visibility;
                }
            }
        }

        private void UpdateToggleButtons()
        {
            var kind = _navCollapsed ? MaterialDesignThemes.Wpf.PackIconKind.ChevronRight : MaterialDesignThemes.Wpf.PackIconKind.ChevronLeft;

            ToggleNavButton.Content = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = kind,
                Width = 18,
                Height = 18
            };

            DashboardToggleNavButton.Content = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = kind,
                Width = 18,
                Height = 18
            };
        }

        private static void SetLabelVisibilityInPanel(UIElement? element, Visibility visibility)
        {
            if (element != null)
            {
                element.Visibility = visibility;
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _ = RenderDashboardChartsAsync();
        }

        private void TrainingNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Development, "Development"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowTraining();
            }
        }

        private void DashboardNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowDashboard();
                _ = RenderDashboardChartsAsync();
            }
        }

        private void RecruitmentNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Recruitment, "Recruitment"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowRecruitment();
            }
        }

        private void PerformanceNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Development, "Development"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowPerformance();
            }
        }

        private void UsersNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Users, "Account"))
            {
                return;
            }

            OpenUsersModule(preferProfileTab: !IsAdminAccess);
        }

        private void UserAccountSettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Users, "Account"))
            {
                return;
            }

            OpenUsersModule(preferProfileTab: true);
        }

        private void OpenUsersModule(bool preferProfileTab)
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            vm.ShowUsers();

            if (UsersRolesModule != null)
            {
                if (preferProfileTab)
                {
                    UsersRolesModule.OpenProfileTab();
                }
                else
                {
                    UsersRolesModule.OpenUsersAdminTab();
                }
            }

            _ = UsersRolesModule?.RefreshAsync();
        }

        private void EmployeesNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Employees, "Employees"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowEmployees();
            }
        }

        private void DepartmentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Departments, "Departments & Positions"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowDepartments();
            }
        }

        private void AttendanceNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Attendance, "Attendance"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowAttendance();
            }
        }

        private void AttendanceLogsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.AttendanceLogs, "Attendance Logs"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowAttendanceLogs();
            }
        }

        private void AdjustmentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Adjustments, "Adjustments"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowAdjustments();
            }
        }

        private void LeaveNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Leave, "Leave"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowLeave();
            }
        }

        private void PayrollNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Payroll, "Payroll"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowPayroll();
            }
        }

        private void MyDocumentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CanOpenPersonalDocuments())
            {
                MessageBox.Show(
                    "Your account must be linked to an employee profile before personal documents can be opened.",
                    "My Documents",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowMyDocuments();
            }

            _ = MyDocumentsModule?.RefreshAsync();
        }

        private void DashboardOpenAttendanceButton_OnClick(object sender, RoutedEventArgs e) =>
            AttendanceNavButton_OnClick(sender, e);

        private void DashboardOpenAdjustmentsButton_OnClick(object sender, RoutedEventArgs e) =>
            AdjustmentsNavButton_OnClick(sender, e);

        private void DashboardOpenLeaveButton_OnClick(object sender, RoutedEventArgs e) =>
            LeaveNavButton_OnClick(sender, e);

        private void DashboardOpenPayrollButton_OnClick(object sender, RoutedEventArgs e) =>
            PayrollNavButton_OnClick(sender, e);

        private void DashboardOpenRecruitmentButton_OnClick(object sender, RoutedEventArgs e) =>
            RecruitmentNavButton_OnClick(sender, e);

        private void DashboardOpenDevelopmentButton_OnClick(object sender, RoutedEventArgs e) =>
            TrainingNavButton_OnClick(sender, e);

        private void DashboardOpenMyDocumentsButton_OnClick(object sender, RoutedEventArgs e) =>
            MyDocumentsNavButton_OnClick(sender, e);

        private void DashboardOpenPendingRequestsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            var stats = vm.Stats;
            if (stats.MyPendingAdjustments > 0)
            {
                DashboardOpenAdjustmentsButton_OnClick(sender, e);
                return;
            }

            if (stats.MyPendingLeaves > 0)
            {
                DashboardOpenLeaveButton_OnClick(sender, e);
                return;
            }

            DashboardOpenAdjustmentsButton_OnClick(sender, e);
        }

        private void DashboardOpenLatestDecisionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            var module = vm.Stats.MyLatestDecisionModule?.Trim().ToUpperInvariant();
            switch (module)
            {
                case "LEAVE":
                    DashboardOpenLeaveButton_OnClick(sender, e);
                    break;
                case "ADJUSTMENT":
                    DashboardOpenAdjustmentsButton_OnClick(sender, e);
                    break;
                default:
                    DashboardOpenAttendanceButton_OnClick(sender, e);
                    break;
            }
        }

        private void DashboardSearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            ExecuteDashboardSearch();
        }

        private void DashboardSearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteDashboardSearch();
                e.Handled = true;
            }
        }

        private void ExecuteDashboardSearch()
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            var query = DashboardSearchTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                vm.ShowDashboard();
                return;
            }

            var q = query.ToLowerInvariant();

            if (q.Contains("dashboard"))
            {
                vm.ShowDashboard();
                return;
            }

            if (q.Contains("employee") || q.Contains("staff"))
            {
                if (EnsureModuleAccess(ModuleKey.Employees, "Employees"))
                {
                    vm.ShowEmployees();
                }
                return;
            }

            if (q.Contains("department") || q.Contains("position"))
            {
                if (EnsureModuleAccess(ModuleKey.Departments, "Departments & Positions"))
                {
                    vm.ShowDepartments();
                }
                return;
            }

            if (q.Contains("adjust") || q.Contains("correction"))
            {
                if (EnsureModuleAccess(ModuleKey.Adjustments, "Adjustments"))
                {
                    vm.ShowAdjustments();
                }
                return;
            }

            if (q.Contains("log"))
            {
                if (EnsureModuleAccess(ModuleKey.AttendanceLogs, "Attendance Logs"))
                {
                    vm.ShowAttendanceLogs();
                }
                return;
            }

            if (q.Contains("attendance") || q.Contains("biometric") || q.Contains("dtr"))
            {
                if (EnsureModuleAccess(ModuleKey.Attendance, "Attendance"))
                {
                    vm.ShowAttendance();
                }
                return;
            }

            if (q.Contains("leave"))
            {
                if (EnsureModuleAccess(ModuleKey.Leave, "Leave"))
                {
                    vm.ShowLeave();
                }
                return;
            }

            if (q.Contains("payroll") || q.Contains("payslip"))
            {
                if (EnsureModuleAccess(ModuleKey.Payroll, "Payroll"))
                {
                    vm.ShowPayroll();
                }
                return;
            }

            if (q.Contains("document") || q.Contains("certificate") || q.Contains("attachment"))
            {
                if (EnsureModuleAccess(ModuleKey.MyDocuments, "My Documents"))
                {
                    vm.ShowMyDocuments();
                }
                return;
            }

            if (q.Contains("recruit") || q.Contains("job") || q.Contains("applicant"))
            {
                if (EnsureModuleAccess(ModuleKey.Recruitment, "Recruitment"))
                {
                    vm.ShowRecruitment();
                }
                return;
            }

            if (q.Contains("development") || q.Contains("training") || q.Contains("performance"))
            {
                if (EnsureModuleAccess(ModuleKey.Development, "Development"))
                {
                    vm.ShowTraining();
                }
                return;
            }

            MessageBox.Show(
                $"No direct module match for \"{query}\".",
                "Search",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void NotificationButton_OnClick(object sender, RoutedEventArgs e)
        {
            DashboardNotificationsPopup.IsOpen = !DashboardNotificationsPopup.IsOpen;
            if (DashboardNotificationsPopup.IsOpen)
            {
                await _notificationsViewModel.RefreshAsync();
            }
        }

        private void CloseNotificationsPopup_OnClick(object sender, RoutedEventArgs e)
        {
            DashboardNotificationsPopup.IsOpen = false;
        }

        private void EmployeeModule_OpenModuleRequested(object? sender, string moduleKey)
        {
            DashboardNotificationsPopup.IsOpen = false;

            var normalized = moduleKey?.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "LEAVE":
                    LeaveNavButton_OnClick(this, new RoutedEventArgs());
                    break;
                case "PAYROLL":
                    PayrollNavButton_OnClick(this, new RoutedEventArgs());
                    break;
                case "DEVELOPMENT":
                    TrainingNavButton_OnClick(this, new RoutedEventArgs());
                    break;
                case "ADJUSTMENTS":
                    AdjustmentsNavButton_OnClick(this, new RoutedEventArgs());
                    break;
                case "ATTENDANCE":
                    AttendanceNavButton_OnClick(this, new RoutedEventArgs());
                    break;
                default:
                    DashboardNavButton_OnClick(this, new RoutedEventArgs());
                    break;
            }
        }
    }
}

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
        private bool _navCollapsed = true;
        private readonly GridLength _expandedWidth = new GridLength(270);
        private readonly GridLength _collapsedWidth = new GridLength(0);
        private readonly Thickness _expandedNavMargin = new Thickness(18, 22, 18, 0);
        private readonly Thickness _collapsedNavMargin = new Thickness(0);
        private readonly DashboardDataService _dashboardDataService = new(DbConfig.ConnectionString);
        private readonly AuthenticatedUser? _authenticatedUser;
        private readonly NotificationsViewModel _notificationsViewModel = new();
        private bool _isRenderingDashboardCharts;
        private bool _isRefreshingAllModules;
        private bool _isSystemRefreshQueued;
        private DateTime _lastSystemRefreshUtc = DateTime.MinValue;
        private static readonly TimeSpan RefreshDebounceDelay = TimeSpan.FromMilliseconds(600);
        private static readonly TimeSpan MinimumRefreshGap = TimeSpan.FromSeconds(8);

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
            Transactions,
            Reports,
            Documents,
            DocumentVerification,
            Recruitment,
            Development,
            Users,
            Beneficiaries
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
            EmployeesModule?.SetCurrentUser(_authenticatedUser);
            AttendanceModule?.SetCurrentUser(_authenticatedUser);
            AttendanceLogsModule?.SetCurrentUser(_authenticatedUser);
            AdjustmentsModule?.SetCurrentUser(_authenticatedUser);
            LeaveModule?.SetCurrentUser(_authenticatedUser);
            PayrollModule?.SetCurrentUser(_authenticatedUser);
            TransactionsModule?.SetCurrentUser(_authenticatedUser);
            ReportsModule?.SetCurrentUser(_authenticatedUser);
            DevelopmentModule?.SetCurrentUser(_authenticatedUser);
            DocumentsModule?.SetCurrentUser(_authenticatedUser);
            DocumentVerificationModule?.SetCurrentUser(_authenticatedUser);
            if (DocumentsModule != null)
            {
                DocumentsModule.OpenModuleRequested += EmployeeModule_OpenModuleRequested;
            }
            _notificationsViewModel.SetCurrentUser(_authenticatedUser);
            _notificationsViewModel.OpenModuleRequested += EmployeeModule_OpenModuleRequested;
            var vm = new DashboardViewModel(_authenticatedUser);
            DataContext = vm;
            DashboardNotificationsPopup.DataContext = _notificationsViewModel;
            vm.PropertyChanged += DashboardViewModel_PropertyChanged;
            Loaded += DashboardWindow_Loaded;
            Unloaded += DashboardWindow_Unloaded;
            ApplyNavigationLayout();
            ApplyCurrentUserCard();
            ApplyRoleBasedNavigation();
        }

        private async void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SystemRefreshBus.DataChanged += SystemRefreshBus_DataChanged;
            await RenderDashboardChartsAsync();
            await _notificationsViewModel.RefreshAsync();
            DashboardNotificationsPopup.IsOpen = true;
        }

        private void DashboardWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemRefreshBus.DataChanged -= SystemRefreshBus_DataChanged;

            if (DataContext is DashboardViewModel vm)
            {
                vm.PropertyChanged -= DashboardViewModel_PropertyChanged;
            }

            if (DocumentsModule != null)
            {
                DocumentsModule.OpenModuleRequested -= EmployeeModule_OpenModuleRequested;
            }

            _notificationsViewModel.OpenModuleRequested -= EmployeeModule_OpenModuleRequested;
        }

        private void SystemRefreshBus_DataChanged(object? sender, SystemDataChangedEventArgs e)
        {
            _ = QueueSystemRefreshAsync();
        }

        private async Task QueueSystemRefreshAsync()
        {
            if (_isSystemRefreshQueued)
            {
                return;
            }

            _isSystemRefreshQueued = true;
            try
            {
                await Task.Delay(RefreshDebounceDelay);

                var elapsed = DateTime.UtcNow - _lastSystemRefreshUtc;
                var remainingGap = MinimumRefreshGap - elapsed;
                if (remainingGap > TimeSpan.Zero)
                {
                    await Task.Delay(remainingGap);
                }

                await Dispatcher.InvokeAsync(RefreshVisibleModulesAsync);
            }
            finally
            {
                _isSystemRefreshQueued = false;
            }
        }

        private async Task RefreshVisibleModulesAsync()
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
                    await RenderDashboardChartsAsync();

                    if (vm.IsEmployeesVisible && EmployeesModule != null)
                    {
                        await EmployeesModule.RefreshAsync();
                    }
                    else if (vm.IsDepartmentsVisible && DepartmentsModule != null)
                    {
                        await DepartmentsModule.RefreshAsync();
                    }
                    else if (vm.IsAttendanceVisible && AttendanceModule != null)
                    {
                        await AttendanceModule.RefreshAsync();
                    }
                    else if (vm.IsAttendanceLogsVisible && AttendanceLogsModule != null)
                    {
                        await AttendanceLogsModule.RefreshAsync();
                    }
                    else if (vm.IsAdjustmentsVisible && AdjustmentsModule != null)
                    {
                        await AdjustmentsModule.RefreshAsync();
                    }
                    else if (vm.IsLeaveVisible && LeaveModule != null)
                    {
                        await LeaveModule.RefreshAsync();
                    }
                    else if (vm.IsPayrollVisible && PayrollModule != null)
                    {
                        await PayrollModule.RefreshAsync();
                    }
                    else if (vm.IsTransactionsVisible && TransactionsModule != null)
                    {
                        await TransactionsModule.RefreshAsync();
                    }
                    else if (vm.IsReportsVisible && ReportsModule != null)
                    {
                        await ReportsModule.RefreshAsync();
                    }
                    else if (vm.IsDocumentsVisible && DocumentsModule != null)
                    {
                        await DocumentsModule.RefreshAsync();
                    }
                    else if (vm.IsDocumentVerificationVisible && DocumentVerificationModule != null)
                    {
                        await DocumentVerificationModule.RefreshAsync();
                    }
                    else if (vm.IsRecruitmentVisible && RecruitmentModule != null)
                    {
                        await RecruitmentModule.RefreshAsync();
                    }
                    else if ((vm.IsTrainingVisible || vm.IsPerformanceVisible) && DevelopmentModule != null)
                    {
                        await DevelopmentModule.RefreshAsync();
                    }
                    else if (vm.IsUsersVisible && UsersRolesModule != null)
                    {
                        await UsersRolesModule.RefreshAsync();
                    }
                }

                await _notificationsViewModel.RefreshAsync();
                _lastSystemRefreshUtc = DateTime.UtcNow;
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
            var isEmployeeAccess = IsEmployeeAccess;
            var canEmployees = CanAccessModule(ModuleKey.Employees);
            var canDepartments = CanAccessModule(ModuleKey.Departments);
            var canAttendance = CanAccessModule(ModuleKey.Attendance);
            var canAttendanceLogs = CanAccessModule(ModuleKey.AttendanceLogs);
            var canAdjustments = CanAccessModule(ModuleKey.Adjustments);
            var canTransactions = CanAccessModule(ModuleKey.Transactions);
            var canReports = CanAccessModule(ModuleKey.Reports);
            var canDocuments = CanAccessModule(ModuleKey.Documents);

            DashboardNavButton.Visibility = Visibility.Visible;
            EmployeeProfileNavButton.Visibility = Visibility.Collapsed;
            EmployeeDepartmentPositionNavButton.Visibility = ToVisibility(isEmployeeAccess && canEmployees);
            EmployeesNavButton.Visibility = ToVisibility(!isEmployeeAccess && (canEmployees || canDepartments));
            if (EmployeesSubNavButton != null)
            {
                EmployeesSubNavButton.Visibility = ToVisibility(!isEmployeeAccess && canEmployees);
            }

            DepartmentsNavButton.Visibility = ToVisibility(!isEmployeeAccess && canDepartments);
            AttendanceNavButton.Visibility = ToVisibility(isEmployeeAccess ? canAttendance : canAttendance || canAttendanceLogs || canAdjustments);
            if (AttendanceSubNavButton != null)
            {
                AttendanceSubNavButton.Visibility = ToVisibility(!isEmployeeAccess && canAttendance);
            }

            AttendanceLogsNavButton.Visibility = ToVisibility(!isEmployeeAccess && canAttendanceLogs);
            AdjustmentsNavButton.Visibility = ToVisibility(!isEmployeeAccess && canAdjustments);
            EmployeeAttendanceLogsNavButton.Visibility = ToVisibility(isEmployeeAccess && canAttendanceLogs);
            EmployeeAdjustmentsNavButton.Visibility = ToVisibility(isEmployeeAccess && canAdjustments);
            LeaveNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Leave));
            PayrollNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Payroll));
            RecordsReportsNavButton.Visibility = ToVisibility(canTransactions || canReports || canDocuments);
            TransactionsNavButton.Visibility = ToVisibility(canTransactions);
            ReportsNavButton.Visibility = ToVisibility(canReports);
            DocumentsNavButton.Visibility = ToVisibility(canDocuments);
            DocumentVerificationNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.DocumentVerification));
            BeneficiariesNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Beneficiaries));
            RecruitmentNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Recruitment));
            DevelopmentNavButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Development));

            if (AttendanceNavLabelText != null)
            {
                AttendanceNavLabelText.Text = isEmployeeAccess ? "My Attendance / DTR" : "Attendance / DTR";
            }

            if (LeaveNavLabelText != null)
            {
                LeaveNavLabelText.Text = isEmployeeAccess ? "My Leave" : "Leave";
            }

            if (PayrollNavLabelText != null)
            {
                PayrollNavLabelText.Text = isEmployeeAccess ? "My Payroll" : "Payroll";
            }

            if (DevelopmentNavLabelText != null)
            {
                DevelopmentNavLabelText.Text = isEmployeeAccess ? "My Development" : "Development";
            }

            if (AttendanceNavChevronIcon != null)
            {
                AttendanceNavChevronIcon.Visibility = isEmployeeAccess ? Visibility.Collapsed : Visibility.Visible;
            }

            if (EmployeesNavButton.Visibility != Visibility.Visible)
            {
                SetSidebarGroupExpanded(EmployeeManagementSubmenuPanel, EmployeesNavChevronIcon, false);
            }

            if (AttendanceNavButton.Visibility != Visibility.Visible || isEmployeeAccess)
            {
                SetSidebarGroupExpanded(AttendanceSubmenuPanel, AttendanceNavChevronIcon, false);
            }

            if (RecordsReportsNavButton.Visibility != Visibility.Visible)
            {
                SetSidebarGroupExpanded(RecordsReportsSubmenuPanel, RecordsReportsNavChevronIcon, false);
            }

            if (EmployeeManagementEmployeesMenuItem != null)
            {
                EmployeeManagementEmployeesMenuItem.Visibility = ToVisibility(!isEmployeeAccess && canEmployees);
            }

            if (EmployeeManagementDepartmentsMenuItem != null)
            {
                EmployeeManagementDepartmentsMenuItem.Visibility = ToVisibility(!isEmployeeAccess && canDepartments);
            }

            if (AttendanceMenuItem != null)
            {
                AttendanceMenuItem.Visibility = ToVisibility(!isEmployeeAccess && canAttendance);
            }

            if (AttendanceLogsMenuItem != null)
            {
                AttendanceLogsMenuItem.Visibility = ToVisibility(!isEmployeeAccess && canAttendanceLogs);
            }

            if (AdjustmentsMenuItem != null)
            {
                AdjustmentsMenuItem.Visibility = ToVisibility(!isEmployeeAccess && canAdjustments);
            }

            if (RecordsReportsTransactionsMenuItem != null)
            {
                RecordsReportsTransactionsMenuItem.Visibility = ToVisibility(canTransactions);
            }

            if (RecordsReportsReportsMenuItem != null)
            {
                RecordsReportsReportsMenuItem.Visibility = ToVisibility(canReports);
            }

            if (RecordsReportsDocumentsMenuItem != null)
            {
                RecordsReportsDocumentsMenuItem.Visibility = ToVisibility(canDocuments);
                RecordsReportsDocumentsMenuItem.Header = isEmployeeAccess ? "My Documents" : "Documents";
            }

            if (QuickEmployeesButton != null)
            {
                QuickEmployeesButton.Visibility = ToVisibility(!isEmployeeAccess && canEmployees);
            }

            if (QuickAttendanceButton != null)
            {
                QuickAttendanceButton.Visibility = ToVisibility(canAttendance);
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

            if (QuickPerformanceButton != null)
            {
                QuickPerformanceButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Development));
            }

            if (QuickDocumentsButton != null)
            {
                QuickDocumentsButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Documents) && CanOpenDocumentsModule());
            }

            if (QuickProfileButton != null)
            {
                QuickProfileButton.Visibility = ToVisibility(CanAccessModule(ModuleKey.Users));
            }

        }

        private bool CanAccessModule(ModuleKey module)
        {
            if (IsAdminAccess)
            {
                return module switch
                {
                    ModuleKey.Documents => true,
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
                    ModuleKey.Transactions => true,
                    ModuleKey.Reports => true,
                    ModuleKey.Documents => true,
                    ModuleKey.DocumentVerification => true,
                    ModuleKey.Recruitment => true,
                    ModuleKey.Development => true,
                    ModuleKey.Users => true, // profile tab only is enforced in UsersRolesWindow
                    ModuleKey.Beneficiaries => true,
                    _ => false
                };
            }

            if (IsEmployeeAccess)
            {
                return module switch
                {
                    ModuleKey.Dashboard => true,
                    ModuleKey.Employees => true,
                    ModuleKey.Attendance => true,
                    ModuleKey.AttendanceLogs => true,
                    ModuleKey.Adjustments => true,
                    ModuleKey.Leave => true,
                    ModuleKey.Payroll => true,
                    ModuleKey.Transactions => true,
                    ModuleKey.Reports => true,
                    ModuleKey.Documents => true,
                    ModuleKey.Development => true,
                    ModuleKey.Users => true, // profile tab only
                    _ => false
                };
            }

            // Guests/default: dashboard + profile only
            return module is ModuleKey.Dashboard or ModuleKey.Users;
        }

        private bool CanOpenDocumentsModule() =>
            IsAdminAccess ||
            IsHrAccess ||
            (_authenticatedUser?.EmployeeId is int employeeId && employeeId > 0);

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

        private static void OpenAttachedMenu(Button? button)
        {
            if (button?.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }

        private static void SetSidebarGroupExpanded(
            FrameworkElement? panel,
            MaterialDesignThemes.Wpf.PackIcon? chevronIcon,
            bool isExpanded)
        {
            if (panel != null)
            {
                panel.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            }

            if (chevronIcon != null)
            {
                chevronIcon.Kind = isExpanded
                    ? MaterialDesignThemes.Wpf.PackIconKind.ChevronUp
                    : MaterialDesignThemes.Wpf.PackIconKind.ChevronDown;
            }
        }

        private void ToggleSidebarGroup(
            FrameworkElement? targetPanel,
            MaterialDesignThemes.Wpf.PackIcon? targetChevron)
        {
            var nextExpanded = targetPanel?.Visibility != Visibility.Visible;
            CollapseSidebarGroups();
            SetSidebarGroupExpanded(targetPanel, targetChevron, nextExpanded);
        }

        private void CollapseSidebarGroups()
        {
            SetSidebarGroupExpanded(EmployeeManagementSubmenuPanel, EmployeesNavChevronIcon, false);
            SetSidebarGroupExpanded(AttendanceSubmenuPanel, AttendanceNavChevronIcon, false);
            SetSidebarGroupExpanded(RecordsReportsSubmenuPanel, RecordsReportsNavChevronIcon, false);
        }

        private void OpenEmployeesModule()
        {
            OpenEmployeesModule(false);
        }

        private void OpenEmployeesModule(bool openProfileTab)
        {
            if (!EnsureModuleAccess(ModuleKey.Employees, "Employees"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowEmployees();
            }

            if (openProfileTab)
            {
                EmployeesModule?.OpenProfileTab();
            }
        }

        private void OpenDepartmentsModule()
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

        private void OpenAttendanceModule()
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

        private void OpenAttendanceLogsModule()
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

        private void OpenAdjustmentsModule()
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
            ApplyNavigationLayout();
        }

        private void ApplyNavigationLayout()
        {
            NavColumn.Width = _navCollapsed ? _collapsedWidth : _expandedWidth;
            SidebarHost.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;

            UpdateToggleButtons();
            SetNavLabelsVisibility(_navCollapsed ? Visibility.Collapsed : Visibility.Visible);

            NavHeaderPanel.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
            UserAccountCard.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
            NavStack.Margin = _navCollapsed ? _collapsedNavMargin : _expandedNavMargin;
            UpdateNavChevronVisibility();
            UpdateNavigationButtonVisuals();

            if (_navCollapsed)
            {
                CollapseSidebarGroups();
            }
        }

        private void UpdateNavChevronVisibility()
        {
            var chevronVisibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;

            if (EmployeesNavChevronIcon != null)
            {
                EmployeesNavChevronIcon.Visibility = chevronVisibility;
            }

            if (AttendanceNavChevronIcon != null)
            {
                AttendanceNavChevronIcon.Visibility = IsEmployeeAccess ? Visibility.Collapsed : chevronVisibility;
            }

            if (RecordsReportsNavChevronIcon != null)
            {
                RecordsReportsNavChevronIcon.Visibility = chevronVisibility;
            }
        }

        private void UpdateNavigationButtonVisuals()
        {
            var subNavStyle = TryFindResource("SubNavButtonStyle") as System.Windows.Style;

            foreach (var button in FindVisualChildren<Button>(NavStack))
            {
                button.HorizontalContentAlignment = _navCollapsed ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Left;
                button.HorizontalAlignment = _navCollapsed ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch;
                button.Width = _navCollapsed ? 44 : double.NaN;
                button.MinWidth = _navCollapsed ? 44 : 0;

                if (_navCollapsed)
                {
                    button.Padding = new Thickness(0);
                }
                else
                {
                    button.Padding = ReferenceEquals(button.Style, subNavStyle)
                        ? new Thickness(16, 10, 12, 10)
                        : new Thickness(14, 12, 14, 12);
                }
            }

            foreach (var icon in FindVisualChildren<MaterialDesignThemes.Wpf.PackIcon>(NavStack))
            {
                icon.Margin = _navCollapsed ? new Thickness(0) : new Thickness(0, 0, 10, 0);
            }
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
            var kind = _navCollapsed ? MaterialDesignThemes.Wpf.PackIconKind.Menu : MaterialDesignThemes.Wpf.PackIconKind.MenuOpen;

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

        private void HelpHeaderButton_OnClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Use the sidebar menu button to open modules. Use Admin Tools for account and system actions, and use the notification bell to review recent activity.",
                "Dashboard Help",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                DevelopmentModule?.ShowTrainingTab();
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
                DevelopmentModule?.ShowPerformanceTab();
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

        private void EmployeeProfileNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Users, "My Profile"))
            {
                return;
            }

            OpenUsersModule(preferProfileTab: true);
        }

        private void EmployeeDepartmentPositionNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Employees, "My Department / Position"))
            {
                return;
            }

            OpenEmployeesModule(openProfileTab: true);
        }

        private void BeneficiariesNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Beneficiaries, "Beneficiary Verification"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowBeneficiaries();
            }
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
            if (ReferenceEquals(sender, QuickEmployeesButton))
            {
                OpenEmployeesModule();
                return;
            }

            if (_navCollapsed)
            {
                OpenAttachedMenu(EmployeesNavButton);
                return;
            }

            ToggleSidebarGroup(EmployeeManagementSubmenuPanel, EmployeesNavChevronIcon);
        }

        private void DepartmentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenDepartmentsModule();
        }

        private void AttendanceNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, QuickAttendanceButton))
            {
                OpenAttendanceModule();
                return;
            }

            if (IsEmployeeAccess)
            {
                OpenAttendanceModule();
                return;
            }

            if (_navCollapsed)
            {
                OpenAttachedMenu(AttendanceNavButton);
                return;
            }

            ToggleSidebarGroup(AttendanceSubmenuPanel, AttendanceNavChevronIcon);
        }

        private void RecordsReportsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_navCollapsed)
            {
                OpenAttachedMenu(RecordsReportsNavButton);
                return;
            }

            ToggleSidebarGroup(RecordsReportsSubmenuPanel, RecordsReportsNavChevronIcon);
        }

        private void AttendanceLogsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenAttendanceLogsModule();
        }

        private void AdjustmentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenAdjustmentsModule();
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

        private void TransactionsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Transactions, "Transactions"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowTransactions();
            }
        }

        private void ReportsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.Reports, "Reports"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowReports();
            }
        }

        private void DocumentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CanOpenDocumentsModule())
            {
                MessageBox.Show(
                    "Your account must be linked to an employee profile before this module can be opened.",
                    "Documents",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowDocuments();
            }

            _ = DocumentsModule?.RefreshAsync();
        }

        private void DocumentVerificationNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureModuleAccess(ModuleKey.DocumentVerification, "System Verifier"))
            {
                return;
            }

            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowDocumentVerification();
            }

            _ = DocumentVerificationModule?.RefreshAsync();
        }

        private void DashboardOpenAttendanceButton_OnClick(object sender, RoutedEventArgs e) =>
            OpenAttendanceModule();

        private void DashboardOpenAdjustmentsButton_OnClick(object sender, RoutedEventArgs e) =>
            OpenAdjustmentsModule();

        private void DashboardOpenLeaveButton_OnClick(object sender, RoutedEventArgs e) =>
            LeaveNavButton_OnClick(sender, e);

        private void DashboardOpenPayrollButton_OnClick(object sender, RoutedEventArgs e) =>
            PayrollNavButton_OnClick(sender, e);

        private void DashboardOpenRecruitmentButton_OnClick(object sender, RoutedEventArgs e) =>
            RecruitmentNavButton_OnClick(sender, e);

        private void DashboardOpenDevelopmentButton_OnClick(object sender, RoutedEventArgs e) =>
            TrainingNavButton_OnClick(sender, e);

        private void DashboardOpenDocumentsButton_OnClick(object sender, RoutedEventArgs e) =>
            DocumentsNavButton_OnClick(sender, e);

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

        private void ExecuteDashboardSearch(string? query = null)
        {
            if (DataContext is not DashboardViewModel vm)
            {
                return;
            }

            query = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                vm.ShowDashboard();
                return;
            }

            var normalizedQuery = query.ToLowerInvariant();

            if (normalizedQuery.Contains("dashboard"))
            {
                vm.ShowDashboard();
                return;
            }

            if (normalizedQuery.Contains("employee") || normalizedQuery.Contains("staff"))
            {
                if (EnsureModuleAccess(ModuleKey.Employees, "Employees"))
                {
                    vm.ShowEmployees();
                }
                return;
            }

            if (normalizedQuery.Contains("department") || normalizedQuery.Contains("position"))
            {
                if (IsEmployeeAccess && EnsureModuleAccess(ModuleKey.Employees, "My Department / Position"))
                {
                    OpenEmployeesModule(openProfileTab: true);
                    return;
                }

                if (EnsureModuleAccess(ModuleKey.Departments, "Departments & Positions"))
                {
                    vm.ShowDepartments();
                }
                return;
            }

            if (normalizedQuery.Contains("adjust") || normalizedQuery.Contains("correction"))
            {
                if (EnsureModuleAccess(ModuleKey.Adjustments, "Adjustments"))
                {
                    vm.ShowAdjustments();
                }
                return;
            }

            if (normalizedQuery.Contains("log"))
            {
                if (EnsureModuleAccess(ModuleKey.AttendanceLogs, "Attendance Logs"))
                {
                    vm.ShowAttendanceLogs();
                }
                return;
            }

            if (normalizedQuery.Contains("attendance") || normalizedQuery.Contains("biometric") || normalizedQuery.Contains("dtr"))
            {
                if (EnsureModuleAccess(ModuleKey.Attendance, "Attendance"))
                {
                    vm.ShowAttendance();
                }
                return;
            }

            if (normalizedQuery.Contains("leave"))
            {
                if (EnsureModuleAccess(ModuleKey.Leave, "Leave"))
                {
                    vm.ShowLeave();
                }
                return;
            }

            if (normalizedQuery.Contains("payroll") || normalizedQuery.Contains("payslip"))
            {
                if (EnsureModuleAccess(ModuleKey.Payroll, "Payroll"))
                {
                    vm.ShowPayroll();
                }
                return;
            }

            if (normalizedQuery.Contains("transaction") || normalizedQuery.Contains("payment") || normalizedQuery.Contains("receipt"))
            {
                if (EnsureModuleAccess(ModuleKey.Transactions, "Transactions"))
                {
                    vm.ShowTransactions();
                }
                return;
            }

            if (normalizedQuery.Contains("report") || normalizedQuery.Contains("export") || normalizedQuery.Contains("analytics"))
            {
                if (EnsureModuleAccess(ModuleKey.Reports, "Reports"))
                {
                    vm.ShowReports();
                }
                return;
            }

            if (normalizedQuery.Contains("document") || normalizedQuery.Contains("certificate") || normalizedQuery.Contains("attachment"))
            {
                if (EnsureModuleAccess(ModuleKey.Documents, IsEmployeeAccess ? "My Documents" : "Documents"))
                {
                    vm.ShowDocuments();
                }
                return;
            }

            if (normalizedQuery.Contains("verifier") || normalizedQuery.Contains("checklist") || normalizedQuery.Contains("compliance"))
            {
                if (EnsureModuleAccess(ModuleKey.DocumentVerification, "System Verifier"))
                {
                    vm.ShowDocumentVerification();
                }
                return;
            }

            if (normalizedQuery.Contains("recruit") || normalizedQuery.Contains("job") || normalizedQuery.Contains("applicant"))
            {
                if (EnsureModuleAccess(ModuleKey.Recruitment, "Recruitment"))
                {
                    vm.ShowRecruitment();
                }
                return;
            }

            if (normalizedQuery.Contains("development") || normalizedQuery.Contains("training") || normalizedQuery.Contains("performance"))
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
                    OpenAdjustmentsModule();
                    break;
                case "ATTENDANCE":
                    OpenAttendanceModule();
                    break;
                default:
                    DashboardNavButton_OnClick(this, new RoutedEventArgs());
                    break;
            }
        }

        private void EmployeeManagementEmployeesMenuItem_OnClick(object sender, RoutedEventArgs e) =>
            OpenEmployeesModule();

        private void EmployeeManagementDepartmentsMenuItem_OnClick(object sender, RoutedEventArgs e) =>
            OpenDepartmentsModule();

        private void AttendanceMenuItem_OnClick(object sender, RoutedEventArgs e) =>
            OpenAttendanceModule();

        private void AttendanceLogsMenuItem_OnClick(object sender, RoutedEventArgs e) =>
            OpenAttendanceLogsModule();

        private void AdjustmentsMenuItem_OnClick(object sender, RoutedEventArgs e) =>
            OpenAdjustmentsModule();
    }
}

using HRMS.ViewModel;
using System.Windows;
using System.Windows.Controls;
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

        public DashboardWindow()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }

        private void ToggleNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            _navCollapsed = !_navCollapsed;
            NavColumn.Width = _navCollapsed ? _collapsedWidth : _expandedWidth;

            UpdateToggleButtons();
            SetNavLabelsVisibility(_navCollapsed ? Visibility.Collapsed : Visibility.Visible);

            // Hide header and quick hint when collapsed
            NavHeaderPanel.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
            QuickHintBorder.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;

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

        }

        private void TrainingNavButton_OnClick(object sender, RoutedEventArgs e)
        {
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
            }
        }

        private void RecruitmentNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowRecruitment();
            }
        }

        private void PerformanceNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowPerformance();
            }
        }

        private void UsersNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowUsers();
            }
        }

        private void EmployeesNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowEmployees();
            }
        }

        private void DepartmentsNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowDepartments();
            }
        }

        private void AttendanceNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowAttendance();
            }
        }

        private void LeaveNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowLeave();
            }
        }

        private void PayrollNavButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.ShowPayroll();
            }
        }
    }
}

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HRMS.Model;
using HRMS.ViewModel;
using System.Linq;

namespace HRMS.View
{
    public partial class EnrollEmployeeWindow : Window
    {
        public TrainingViewModel? TrainingVm { get; set; }

        public EnrollEmployeeWindow()
        {
            InitializeComponent();
            Loaded += EnrollEmployeeWindow_Loaded;
        }

        private async void EnrollEmployeeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var service = new TrainingDataService(DbConfig.ConnectionString);
            EmployeeBox.ItemsSource = await service.GetEmployeesAsync();

            // Use courses already in VM if available, otherwise fetch
            var courses = (TrainingVm != null && TrainingVm.Courses.Count > 0)
                ? TrainingVm.Courses.Cast<object>().ToList()
                : (await service.GetCoursesAsync()).Cast<object>().ToList();

            // Keep only active courses
            var activeCourses = courses.Where(c =>
            {
                if (c is TrainingCourseSummary tcs)
                    return string.Equals(tcs.Status, "Active", System.StringComparison.OrdinalIgnoreCase);
                if (c is TrainingCourseDto dto)
                    return string.Equals(dto.Status, "Active", System.StringComparison.OrdinalIgnoreCase);
                return false;
            }).ToList();

            CourseBox.ItemsSource = activeCourses;

            DatePicker.SelectedDate = DateTime.Today;
        }

        private async void Enroll_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeBox.SelectedValue is null || CourseBox.SelectedValue is null)
            {
                MessageBox.Show("Please select both an employee and a course.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var employeeId = (int)EmployeeBox.SelectedValue;
            var courseIdObj = CourseBox.SelectedValue;
            int courseId = courseIdObj is TrainingCourseSummary cs ? cs.Id : Convert.ToInt32(courseIdObj, CultureInfo.InvariantCulture);

            // Validate selected course is active
            string selectedStatus = courseIdObj switch
            {
                TrainingCourseSummary cs2 => cs2.Status,
                TrainingCourseDto dto => dto.Status,
                _ => "Active"
            };
            if (!string.Equals(selectedStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("You can only enroll into Active courses.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var status = (StatusBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Enrolled";
            var date = DatePicker.SelectedDate ?? DateTime.Today;

            try
            {
                var service = new TrainingDataService(DbConfig.ConnectionString);
                var enrollmentId = await service.AddEnrollmentAsync(employeeId, courseId, date, status);

                // Update UI view model
                if (TrainingVm != null)
                {
                    var courseTitle = CourseBox.SelectedItem is TrainingCourseSummary tcs ? tcs.Title : (CourseBox.Text ?? string.Empty);
                    var employeeName = EmployeeBox.Text;
                    var statusLabel = status.ToLowerInvariant() switch
                    {
                        "complete" => "Completed",
                        "reject" => "Rejected",
                        _ => status
                    };
                    var enrollment = new TrainingEnrollment
                    {
                        EnrollmentId = enrollmentId,
                        Employee = employeeName,
                        Course = courseTitle,
                        ScheduleDate = date,
                        Status = statusLabel,
                        StatusColor = TrainingViewModel.GetStatusBrush(statusLabel)
                    };
                    TrainingVm.AddEnrollment(enrollment);
                }

                SystemRefreshBus.Raise("TrainingEnrollmentAdded");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to enroll employee: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                (Keyboard.FocusedElement as UIElement)?.MoveFocus(request);
            }
        }
    }
}

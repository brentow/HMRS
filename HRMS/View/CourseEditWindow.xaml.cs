using System.Windows;
using HRMS.ViewModel;
using HRMS.Model;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HRMS.View
{
    public partial class CourseEditWindow : Window
    {
        public TrainingViewModel? TrainingVm { get; set; }

        public CourseEditWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is TrainingCourseSummary course)
                {
                    // persist to DB
                    var service = new TrainingDataService(DbConfig.ConnectionString);
                    var dto = new TrainingCourseDto(course.Id, course.Title, course.Provider, course.Description, course.Hours, course.Status);
                    await service.UpdateCourseAsync(dto);

                    // update status color in UI immediately
                    course.StatusColor = TrainingViewModel.GetStatusBrush(course.Status);
                    TrainingVm?.RefreshFilter();
                }

                SystemRefreshBus.Raise("TrainingCourseUpdated");
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to save course: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is TrainingCourseSummary course)
                {
                    var service = new TrainingDataService(DbConfig.ConnectionString);
                    await service.DeleteCourseAsync(course.Id);

                    // notify owner view model to remove course from collections
                    TrainingVm?.RemoveCourse(course.Id);
                    SystemRefreshBus.Raise("TrainingCourseDeleted");
                    DialogResult = true;
                    Close();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to delete course: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

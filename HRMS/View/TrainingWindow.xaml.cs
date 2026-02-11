using HRMS.ViewModel;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class TrainingWindow : UserControl
    {
        public TrainingWindow()
        {
            InitializeComponent();
            DataContext = new TrainingViewModel();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void EditCourses_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new CourseEditWindow
            {
                Owner = System.Windows.Window.GetWindow(this),
                TrainingVm = DataContext as TrainingViewModel
            };
            dlg.ShowDialog();
        }

        private void CourseEdit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var course = (sender as System.Windows.FrameworkElement)?.DataContext as TrainingCourseSummary;
            var dlg = new CourseEditWindow
            {
                Owner = System.Windows.Window.GetWindow(this),
                DataContext = course,
                TrainingVm = DataContext as TrainingViewModel
            };
            dlg.ShowDialog();
        }

        private void AddCourse_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new AddCourseWindow
            {
                Owner = System.Windows.Window.GetWindow(this),
                TrainingVm = DataContext as TrainingViewModel
            };
            dlg.ShowDialog();
        }

        private void EnrollEmployee_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new EnrollEmployeeWindow
            {
                Owner = System.Windows.Window.GetWindow(this),
                TrainingVm = DataContext as TrainingViewModel
            };
            dlg.ShowDialog();
        }
    }
}

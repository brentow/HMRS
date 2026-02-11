using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HRMS.Model;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class AddCourseWindow : Window
    {
        public TrainingViewModel? TrainingVm { get; set; }

        public AddCourseWindow()
        {
            InitializeComponent();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleBox.Text?.Trim() ?? string.Empty;
            var provider = ProviderBox.Text?.Trim() ?? string.Empty;
            var description = DescriptionBox.Text?.Trim() ?? string.Empty;
            var status = (StatusBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Active";

            if (!double.TryParse(HoursBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var hours))
            {
                MessageBox.Show("Please enter a valid number for hours.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dto = new TrainingCourseDto(0, title, provider, description, hours, status);
            var service = new TrainingDataService(DbConfig.ConnectionString);
            var newId = await service.AddCourseAsync(dto);

            if (TrainingVm != null)
            {
                var course = new TrainingCourseSummary
                {
                    Id = newId,
                    Title = title,
                    Provider = provider,
                    Description = description,
                    Hours = hours,
                    Status = status,
                    StatusColor = TrainingViewModel.GetStatusBrush(status)
                };
                TrainingVm.AddCourse(course);
            }

            DialogResult = true;
            Close();
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

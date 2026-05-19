using HRMS.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace HRMS.View
{
    public partial class BiometricEnrollmentDialog : Window
    {
        private static readonly Brush SuccessColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));
        private static readonly Brush InfoColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4368"));

        private readonly AttendanceDataService _dataService = new(DbConfig.ConnectionString);
        private readonly BiometricHardwareService _hardwareService = new();
        private readonly DigitalPersonaRuntimeService _captureService = new();

        private int _employeeId;
        private string _employeeNo = string.Empty;
        private string _employeeName = string.Empty;
        private string _biometricUserId = string.Empty;
        private int? _deviceId;
        private bool _captureComplete;

        public BiometricEnrollmentDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Call this before ShowDialog to set the employee being enrolled.
        /// </summary>
        public void SetEmployee(int employeeId, string employeeNo, string employeeName)
        {
            _employeeId = employeeId;
            _employeeNo = employeeNo;
            _employeeName = employeeName;
            _biometricUserId = $"BIO-{employeeNo}-{DateTime.Now:yyyyMMddHHmmss}";

            EmployeeLabel.Text = $"{employeeNo} - {employeeName}";
            BiometricIdLabel.Text = $"Biometric ID: {_biometricUserId}";
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            await CheckReaderConnectionAsync();
        }

        private async Task CheckReaderConnectionAsync()
        {
            try
            {
                var probe = await _hardwareService.ProbeAsync();

                if (probe.HasConnectedReaders)
                {
                    ReaderStatusText.Text = $"✓ Reader connected: {probe.Readers[0].DeviceName}";
                    ReaderStatusText.Foreground = SuccessColor;
                    ReaderDetailText.Text = probe.HasDigitalPersonaSdkRuntime
                        ? "Ready for fingerprint capture."
                        : "Reader detected but SDK runtime not found. Capture may not work.";
                    StartCaptureButton.IsEnabled = true;

                    // Auto-select the first active device
                    var devices = await _dataService.GetBiometricDevicesAsync();
                    var activeDevice = devices.FirstOrDefault(d => d.IsActive);
                    _deviceId = activeDevice?.DeviceId;
                }
                else
                {
                    ReaderStatusText.Text = "✗ No biometric reader connected";
                    ReaderStatusText.Foreground = ErrorColor;
                    ReaderDetailText.Text = "Plug in the fingerprint reader and try again.";
                    StartCaptureButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                ReaderStatusText.Text = "Error checking reader";
                ReaderStatusText.Foreground = ErrorColor;
                ReaderDetailText.Text = ex.Message;
                StartCaptureButton.IsEnabled = false;
            }
        }

        private async void StartCapture_Click(object sender, RoutedEventArgs e)
        {
            if (_employeeId <= 0)
            {
                SetResult("Save the employee first before enrolling biometrics.", false);
                return;
            }

            StartCaptureButton.IsEnabled = false;
            CaptureInstructionText.Text = "Place finger on the reader now...";
            CaptureProgressText.Text = "Scan 0 of 3";
            CaptureDetailText.Text = "Waiting for first fingerprint sample.";

            try
            {
                var capture = await _captureService.CaptureTemplateAsync(progress =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var completed = Math.Max(0, progress.CompletedSamples);
                        var target = Math.Max(1, progress.TargetSamples);
                        CaptureProgressText.Text = $"Scan {Math.Min(completed, target)} of {target}";
                        CaptureDetailText.Text = progress.Message;

                        if (completed >= target)
                        {
                            CaptureInstructionText.Text = "Capture complete!";
                        }
                        else
                        {
                            CaptureInstructionText.Text = "Place finger on the reader again...";
                        }
                    });
                });

                // Save to database
                CaptureInstructionText.Text = "Saving enrollment to database...";
                CaptureProgressText.Text = "Done!";

                await _dataService.AddBiometricEnrollmentAsync(
                    _employeeId,
                    _biometricUserId,
                    _deviceId,
                    "ACTIVE",
                    capture.TemplateData,
                    capture.TemplateFormat,
                    capture.TemplateEncoding,
                    capture.QualityScore);

                _captureComplete = true;
                SystemRefreshBus.Raise("BiometricEnrollmentAdded");

                CaptureInstructionText.Text = "Enrollment saved successfully!";
                CaptureDetailText.Text = $"Quality: {(capture.QualityScore.HasValue ? capture.QualityScore.Value.ToString() : "N/A")} | Reader: {capture.ReaderName}";
                SetResult($"Biometric enrolled for {_employeeName}. Saved to live database.", true);
            }
            catch (OperationCanceledException ex)
            {
                CaptureProgressText.Text = "Cancelled";
                CaptureInstructionText.Text = "Capture was cancelled.";
                SetResult(ex.Message, false);
                StartCaptureButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CaptureProgressText.Text = "Failed";
                CaptureInstructionText.Text = "Capture failed.";
                SetResult($"Error: {ex.Message}", false);
                StartCaptureButton.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void SetResult(string message, bool success)
        {
            ResultText.Text = message;
            ResultText.Foreground = success ? SuccessColor : ErrorColor;
        }
    }
}

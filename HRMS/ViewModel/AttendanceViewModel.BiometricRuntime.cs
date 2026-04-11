using HRMS.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public partial class AttendanceViewModel
    {
        private readonly DigitalPersonaRuntimeService _digitalPersonaRuntimeService = new();

        private int? _selectedLivePunchDeviceId;
        private string _liveScannerTitle = "Live attendance scanner";
        private string _liveScannerStatusText = "Select a registered device, then scan a fingerprint to log attendance.";
        private string _lastLivePunchEmployee = "-";
        private string _lastLivePunchSummary = "No biometric tap has been captured in this session.";
        private Brush _liveScannerBrush = InfoBrush;

        private byte[]? _pendingEnrollmentTemplateData;
        private string _pendingEnrollmentTemplateFormat = string.Empty;
        private string _pendingEnrollmentTemplateEncoding = "BINARY";
        private int? _pendingEnrollmentTemplateQuality;

        public int? SelectedLivePunchDeviceId
        {
            get => _selectedLivePunchDeviceId;
            set
            {
                if (_selectedLivePunchDeviceId == value)
                {
                    return;
                }

                _selectedLivePunchDeviceId = value;
                OnPropertyChanged();
            }
        }

        public string LiveScannerTitle
        {
            get => _liveScannerTitle;
            private set
            {
                if (_liveScannerTitle == value)
                {
                    return;
                }

                _liveScannerTitle = value;
                OnPropertyChanged();
            }
        }

        public string LiveScannerStatusText
        {
            get => _liveScannerStatusText;
            private set
            {
                if (_liveScannerStatusText == value)
                {
                    return;
                }

                _liveScannerStatusText = value;
                OnPropertyChanged();
            }
        }

        public string LastLivePunchEmployee
        {
            get => _lastLivePunchEmployee;
            private set
            {
                if (_lastLivePunchEmployee == value)
                {
                    return;
                }

                _lastLivePunchEmployee = value;
                OnPropertyChanged();
            }
        }

        public string LastLivePunchSummary
        {
            get => _lastLivePunchSummary;
            private set
            {
                if (_lastLivePunchSummary == value)
                {
                    return;
                }

                _lastLivePunchSummary = value;
                OnPropertyChanged();
            }
        }

        public Brush LiveScannerBrush
        {
            get => _liveScannerBrush;
            private set
            {
                if (Equals(_liveScannerBrush, value))
                {
                    return;
                }

                _liveScannerBrush = value;
                OnPropertyChanged();
            }
        }

        public ICommand ScanAttendanceCommand { get; private set; } = null!;

        private void InitializeBiometricRuntimeCommands()
        {
            ScanAttendanceCommand = new AsyncRelayCommand(_ => ScanAttendanceAsync());
        }

        private void SyncBiometricRuntimeSelections()
        {
            if (!SelectedLivePunchDeviceId.HasValue || !DeviceOptions.Any(x => x.Id == SelectedLivePunchDeviceId.Value && x.Id > 0))
            {
                SelectedLivePunchDeviceId = BiometricDevices.FirstOrDefault(x => x.IsActive)?.DeviceId
                    ?? DeviceOptions.FirstOrDefault(x => x.Id > 0)?.Id;
            }
        }

        private void ResetPendingEnrollmentTemplate()
        {
            _pendingEnrollmentTemplateData = null;
            _pendingEnrollmentTemplateFormat = string.Empty;
            _pendingEnrollmentTemplateEncoding = "BINARY";
            _pendingEnrollmentTemplateQuality = null;
        }

        private void ApplyLiveScannerHardwareState(BiometricHardwareProbeResult probe)
        {
            var registeredActiveDevice = BiometricDevices.FirstOrDefault(x => x.IsActive);
            if (!probe.HasConnectedReaders)
            {
                if (registeredActiveDevice != null)
                {
                    LiveScannerTitle = "Registered scanner ready";
                    LiveScannerStatusText = $"{registeredActiveDevice.DeviceName} is active in HRMS. You can use it for enrollment and attendance scanning.";
                    LiveScannerBrush = SuccessBrush;
                }
                else
                {
                    LiveScannerTitle = "No connected scanner";
                    LiveScannerStatusText = "Plug in the U.are.U 4500 reader to start live attendance logging.";
                    LiveScannerBrush = ErrorBrush;
                }
                return;
            }

            if (!probe.HasDigitalPersonaSdkRuntime)
            {
                LiveScannerTitle = "Reader detected, runtime missing";
                LiveScannerStatusText = "The fingerprint reader is connected, but the HID DigitalPersona SDK/runtime is still missing on this PC.";
                LiveScannerBrush = WarningBrush;
                return;
            }

            LiveScannerTitle = "Scanner ready";
            LiveScannerStatusText = "Select the registered device, then click Scan Attendance and place a finger on the reader.";
            LiveScannerBrush = SuccessBrush;
        }

        private async Task ScanAttendanceAsync()
        {
            if (!EnsureAdminOrHrAccess("Scanning biometric attendance"))
            {
                return;
            }

            if (!SelectedLivePunchDeviceId.HasValue || SelectedLivePunchDeviceId.Value <= 0)
            {
                LiveScannerTitle = "Select a device";
                LiveScannerStatusText = "Choose the registered biometric device that should receive the attendance log.";
                LiveScannerBrush = ErrorBrush;
                SetMessage("Select a registered biometric device first.", ErrorBrush);
                return;
            }

            var selectedDevice = BiometricDevices.FirstOrDefault(x => x.DeviceId == SelectedLivePunchDeviceId.Value);
            if (selectedDevice == null)
            {
                LiveScannerTitle = "Device not found";
                LiveScannerStatusText = "Refresh the attendance module and select the device again.";
                LiveScannerBrush = ErrorBrush;
                SetMessage("The selected biometric device is no longer available.", ErrorBrush);
                return;
            }

            var gallery = await _dataService.GetBiometricTemplateGalleryAsync(SelectedLivePunchDeviceId);
            if (gallery.Count == 0)
            {
                LiveScannerTitle = "No enrolled templates";
                LiveScannerStatusText = "Enroll employee fingerprints first before using the live attendance scanner.";
                LiveScannerBrush = WarningBrush;
                SetMessage("No saved fingerprint templates were found for matching.", WarningBrush);
                return;
            }

            try
            {
                LiveScannerTitle = "Waiting for fingerprint";
                LiveScannerStatusText = "Place a finger on the reader now.";
                LiveScannerBrush = InfoBrush;
                SetMessage("Waiting for biometric fingerprint scan...", InfoBrush);

                var match = await _digitalPersonaRuntimeService.IdentifyAsync(
                    gallery.Select(x => new BiometricStoredTemplate(
                        x.EnrollmentId,
                        x.EmployeeId,
                        x.EmployeeNo,
                        x.EmployeeName,
                        x.BiometricUserId,
                        x.DeviceId,
                        x.DeviceName,
                        x.TemplateData,
                        x.TemplateFormat,
                        x.TemplateEncoding)).ToList());

                if (match == null)
                {
                    LiveScannerTitle = "No match found";
                    LiveScannerStatusText = "The scanned fingerprint did not match any active biometric enrollment in HRMS.";
                    LiveScannerBrush = WarningBrush;
                    LastLivePunchSummary = "Fingerprint scan completed, but no employee match was found.";
                    SetMessage("Fingerprint captured, but no matching employee was found.", WarningBrush);
                    return;
                }

                var punchTime = DateTime.Now;
                var lastLogType = await _dataService.GetLatestAttendanceLogTypeAsync(match.Enrollment.EmployeeId, punchTime);
                var nextLogType = ResolveNextBiometricLogType(lastLogType);

                await _dataService.AddAttendanceLogAsync(
                    match.Enrollment.EmployeeId,
                    SelectedLivePunchDeviceId,
                    punchTime,
                    nextLogType,
                    "BIOMETRIC");

                await _dataService.MarkDeviceSyncedNowAsync(SelectedLivePunchDeviceId.Value);

                LastLivePunchEmployee = $"{match.Enrollment.EmployeeNo} - {match.Enrollment.EmployeeName}";
                LastLivePunchSummary = $"{nextLogType} logged at {punchTime:MMM dd, yyyy hh:mm tt} via {selectedDevice.DeviceName}.";
                await RefreshAsync();
                LiveScannerTitle = "Attendance logged";
                LiveScannerStatusText = $"Matched {match.Enrollment.EmployeeName} ({match.Enrollment.BiometricUserId}) with score {match.MatchScore}.";
                LiveScannerBrush = SuccessBrush;
                SetMessage($"Biometric {nextLogType} logged for {match.Enrollment.EmployeeName}.", SuccessBrush);
            }
            catch (Exception ex)
            {
                LiveScannerTitle = "Scan failed";
                LiveScannerStatusText = ex.Message;
                LiveScannerBrush = ErrorBrush;
                SetMessage($"Unable to scan biometric attendance: {ex.Message}", ErrorBrush);
            }
        }

        private static string ResolveNextBiometricLogType(string? lastLogType)
        {
            var normalized = (lastLogType ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                "IN" => "OUT",
                "BREAK_OUT" => "OUT",
                _ => "IN"
            };
        }
    }
}

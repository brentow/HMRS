using HRMS.Model;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HRMS.View
{
    public partial class BiometricDeviceDialog : Window
    {
        private readonly AttendanceDataService _dataService = new(DbConfig.ConnectionString);
        private readonly BiometricHardwareService _hardwareService = new();
        private BiometricHardwareProbeResult? _lastProbe;

        public ObservableCollection<DeviceRowVm> Devices { get; } = new();

        public BiometricDeviceDialog()
        {
            InitializeComponent();
            DevicesGrid.ItemsSource = Devices;
            Loaded += async (_, _) => await LoadDevicesAsync();
        }

        private async void DetectReader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _lastProbe = await _hardwareService.ProbeAsync();
                ReaderStatusTitle.Text = _lastProbe.StatusTitle;
                ReaderStatusTitle.Foreground = new SolidColorBrush(
                    _lastProbe.HasConnectedReaders
                        ? (Color)ColorConverter.ConvertFromString("#2E9D5B")
                        : (Color)ColorConverter.ConvertFromString("#D84343"));
                ReaderStatusText.Text = _lastProbe.StatusText;
                ReaderGuidanceText.Text = _lastProbe.GuidanceText;
                RegisterReaderButton.IsEnabled = _lastProbe.HasConnectedReaders;

                if (_lastProbe.HasConnectedReaders)
                {
                    var reader = _lastProbe.Readers[0];
                    DeviceNameBox.Text = reader.DeviceName;
                    SerialNoBox.Text = reader.SuggestedSerialNo;
                    LocationBox.Text = reader.SuggestedLocation;
                }

                SetStatus(_lastProbe.HasConnectedReaders
                    ? $"Detected: {_lastProbe.Readers[0].DeviceName}"
                    : "No reader detected.", _lastProbe.HasConnectedReaders);
            }
            catch (Exception ex)
            {
                SetStatus($"Detection failed: {ex.Message}", false);
            }
        }

        private async void RegisterReader_Click(object sender, RoutedEventArgs e)
        {
            if (_lastProbe == null || !_lastProbe.HasConnectedReaders)
            {
                SetStatus("Detect a reader first.", false);
                return;
            }

            var reader = _lastProbe.Readers[0];
            try
            {
                await _dataService.AddDeviceAsync(
                    reader.DeviceName,
                    reader.SuggestedSerialNo,
                    reader.SuggestedLocation,
                    string.Empty,
                    true);

                SystemRefreshBus.Raise("BiometricDeviceAdded");
                await LoadDevicesAsync();
                SetStatus($"Registered: {reader.DeviceName}", true);
            }
            catch (Exception ex)
            {
                SetStatus($"Register failed: {ex.Message}", false);
            }
        }

        private async void AddDevice_Click(object sender, RoutedEventArgs e)
        {
            var name = (DeviceNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                SetStatus("Device name is required.", false);
                return;
            }

            try
            {
                await _dataService.AddDeviceAsync(
                    name,
                    (SerialNoBox.Text ?? string.Empty).Trim(),
                    (LocationBox.Text ?? string.Empty).Trim(),
                    (IpAddressBox.Text ?? string.Empty).Trim(),
                    IsActiveCheck.IsChecked == true);

                SystemRefreshBus.Raise("BiometricDeviceAdded");
                await LoadDevicesAsync();
                SetStatus($"Device '{name}' added.", true);
                DeviceNameBox.Clear();
                SerialNoBox.Clear();
                LocationBox.Clear();
                IpAddressBox.Clear();
            }
            catch (Exception ex)
            {
                SetStatus($"Failed: {ex.Message}", false);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async System.Threading.Tasks.Task LoadDevicesAsync()
        {
            try
            {
                var devices = await _dataService.GetBiometricDevicesAsync();
                Devices.Clear();
                foreach (var d in devices)
                {
                    Devices.Add(new DeviceRowVm(d.DeviceName, d.SerialNo, d.Location, d.IpAddress, d.IsActive, d.LastSyncAt));
                }
            }
            catch { }
        }

        private void SetStatus(string msg, bool success)
        {
            StatusText.Text = msg;
            StatusText.Foreground = new SolidColorBrush(
                success
                    ? (Color)ColorConverter.ConvertFromString("#2E9D5B")
                    : (Color)ColorConverter.ConvertFromString("#D84343"));
        }

        public sealed class DeviceRowVm
        {
            public string DeviceName { get; }
            public string SerialNo { get; }
            public string Location { get; }
            public string IpAddress { get; }
            public string StatusText { get; }
            public string LastSyncText { get; }

            public DeviceRowVm(string deviceName, string serialNo, string location, string ipAddress, bool isActive, DateTime? lastSync)
            {
                DeviceName = deviceName ?? "-";
                SerialNo = string.IsNullOrWhiteSpace(serialNo) ? "-" : serialNo;
                Location = string.IsNullOrWhiteSpace(location) ? "-" : location;
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "-" : ipAddress;
                StatusText = isActive ? "ACTIVE" : "INACTIVE";
                LastSyncText = lastSync.HasValue ? lastSync.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture) : "-";
            }
        }
    }
}

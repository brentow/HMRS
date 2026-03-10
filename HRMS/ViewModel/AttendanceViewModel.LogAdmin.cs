using HRMS.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HRMS.ViewModel
{
    public partial class AttendanceViewModel
    {
        private int? _selectedManualLogEmployeeId;
        private int _selectedManualLogDeviceId;
        private DateTime _manualLogDate = DateTime.Today;
        private string _manualLogTime = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        private string _manualLogType = "IN";
        private string _manualLogSource = "MANUAL";

        public ObservableCollection<string> ManualLogTypes { get; } = new()
        {
            "IN",
            "OUT",
            "BREAK_IN",
            "BREAK_OUT"
        };

        public ObservableCollection<string> ManualLogSources { get; } = new()
        {
            "MANUAL",
            "IMPORT",
            "BIOMETRIC"
        };

        public ICommand AddManualLogCommand { get; private set; } = null!;
        public ICommand ImportLogsCsvCommand { get; private set; } = null!;
        public ICommand ExportLogsCsvCommand { get; private set; } = null!;

        public int? SelectedManualLogEmployeeId
        {
            get => _selectedManualLogEmployeeId;
            set
            {
                if (_selectedManualLogEmployeeId == value)
                {
                    return;
                }

                _selectedManualLogEmployeeId = value;
                OnPropertyChanged();
            }
        }

        public int SelectedManualLogDeviceId
        {
            get => _selectedManualLogDeviceId;
            set
            {
                if (_selectedManualLogDeviceId == value)
                {
                    return;
                }

                _selectedManualLogDeviceId = value;
                OnPropertyChanged();
            }
        }

        public DateTime ManualLogDate
        {
            get => _manualLogDate;
            set
            {
                if (_manualLogDate == value)
                {
                    return;
                }

                _manualLogDate = value;
                OnPropertyChanged();
            }
        }

        public string ManualLogTime
        {
            get => _manualLogTime;
            set
            {
                if (_manualLogTime == value)
                {
                    return;
                }

                _manualLogTime = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string ManualLogType
        {
            get => _manualLogType;
            set
            {
                if (_manualLogType == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _manualLogType = value;
                OnPropertyChanged();
            }
        }

        public string ManualLogSource
        {
            get => _manualLogSource;
            set
            {
                if (_manualLogSource == value || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _manualLogSource = value;
                OnPropertyChanged();
            }
        }

        private void InitializeLogsAdmin()
        {
            AddManualLogCommand = new AsyncRelayCommand(_ => AddManualLogAsync());
            ImportLogsCsvCommand = new AsyncRelayCommand(_ => ImportLogsCsvAsync());
            ExportLogsCsvCommand = new AsyncRelayCommand(_ => ExportLogsCsvAsync());

            ManualLogType = ManualLogTypes.First();
            ManualLogSource = ManualLogSources.First();
            SelectedManualLogDeviceId = 0;
        }

        private void SyncLogsAdminLookups()
        {
            if (SelectedManualLogEmployeeId.HasValue &&
                !EmployeeOptions.Any(x => x.Id == SelectedManualLogEmployeeId.Value))
            {
                SelectedManualLogEmployeeId = null;
            }

            if (!DeviceOptions.Any(x => x.Id == SelectedManualLogDeviceId))
            {
                SelectedManualLogDeviceId = 0;
            }
        }

        private async Task AddManualLogAsync()
        {
            if (!SelectedManualLogEmployeeId.HasValue || SelectedManualLogEmployeeId.Value <= 0)
            {
                SetMessage("Select an employee for manual log entry.", ErrorBrush);
                return;
            }

            if (!TryParseTime(ManualLogTime, out var parsedTime))
            {
                SetMessage("Time format should be HH:mm (example: 07:00).", ErrorBrush);
                return;
            }

            try
            {
                var logTime = ManualLogDate.Date + parsedTime;

                await _dataService.AddAttendanceLogAsync(
                    employeeId: SelectedManualLogEmployeeId.Value,
                    deviceId: SelectedManualLogDeviceId > 0 ? SelectedManualLogDeviceId : null,
                    logTime: logTime,
                    logType: ManualLogType,
                    source: ManualLogSource);

                await RefreshAsync();
                SetMessage("Manual attendance log saved.", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceLogAdded");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save manual log: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ImportLogsCsvAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Import Attendance Logs CSV",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    CheckFileExists = true
                };

                var picked = dialog.ShowDialog();
                if (picked != true || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    SetMessage("Attendance import canceled.", InfoBrush);
                    return;
                }

                var lines = await File.ReadAllLinesAsync(dialog.FileName);
                if (lines.Length <= 1)
                {
                    SetMessage("CSV has no data rows.", ErrorBrush);
                    return;
                }

                var employees = await _dataService.GetEmployeesLookupAsync();
                var devices = await _dataService.GetBiometricDevicesAsync();
                var employeeMap = employees.ToDictionary(
                    x => x.EmployeeNo.Trim().ToUpperInvariant(),
                    x => x.EmployeeId);
                var deviceMap = devices.ToDictionary(
                    x => x.DeviceName.Trim().ToUpperInvariant(),
                    x => x.DeviceId);

                var header = ParseCsvLine(lines[0]);
                var idxEmployeeNo = FindHeaderIndex(header, "employee_no", "emp_no", "employee no", "employee");
                var idxDate = FindHeaderIndex(header, "date", "work_date", "log_date");
                var idxTime = FindHeaderIndex(header, "time", "log_time");
                var idxType = FindHeaderIndex(header, "type", "log_type");
                var idxSource = FindHeaderIndex(header, "source");
                var idxDevice = FindHeaderIndex(header, "device", "device_name");

                if (idxEmployeeNo < 0 || idxDate < 0 || idxTime < 0 || idxType < 0)
                {
                    SetMessage("CSV requires columns: EmployeeNo, Date, Time, Type.", ErrorBrush);
                    return;
                }

                var toInsert = new List<AttendanceLogInsertDto>();
                var skipped = 0;

                for (var i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var fields = ParseCsvLine(line);
                    var employeeNo = GetField(fields, idxEmployeeNo).Trim();
                    if (string.IsNullOrWhiteSpace(employeeNo) ||
                        !employeeMap.TryGetValue(employeeNo.ToUpperInvariant(), out var employeeId))
                    {
                        skipped++;
                        continue;
                    }

                    var dateText = GetField(fields, idxDate).Trim();
                    var timeText = GetField(fields, idxTime).Trim();
                    if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue) &&
                        !DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateValue))
                    {
                        skipped++;
                        continue;
                    }

                    if (!TryParseTime(timeText, out var parsedTime))
                    {
                        skipped++;
                        continue;
                    }

                    var typeText = GetField(fields, idxType).Trim();
                    if (string.IsNullOrWhiteSpace(typeText))
                    {
                        typeText = "IN";
                    }

                    var sourceText = idxSource >= 0 ? GetField(fields, idxSource).Trim() : "IMPORT";
                    if (string.IsNullOrWhiteSpace(sourceText))
                    {
                        sourceText = "IMPORT";
                    }

                    var deviceName = idxDevice >= 0 ? GetField(fields, idxDevice).Trim() : string.Empty;
                    int? deviceId = null;
                    if (!string.IsNullOrWhiteSpace(deviceName) &&
                        deviceMap.TryGetValue(deviceName.ToUpperInvariant(), out var matchedDeviceId))
                    {
                        deviceId = matchedDeviceId;
                    }

                    toInsert.Add(new AttendanceLogInsertDto(
                        EmployeeId: employeeId,
                        DeviceId: deviceId,
                        LogTime: dateValue.Date + parsedTime,
                        LogType: typeText,
                        Source: sourceText));
                }

                if (toInsert.Count == 0)
                {
                    SetMessage("No valid rows found for import.", ErrorBrush);
                    return;
                }

                var inserted = await _dataService.AddAttendanceLogsBulkAsync(toInsert);
                await RefreshAsync();
                SetMessage($"Imported {inserted} logs. Skipped {skipped} row(s).", SuccessBrush);
                SystemRefreshBus.Raise("AttendanceLogImported");
            }
            catch (Exception ex)
            {
                SetMessage($"Import failed: {ex.Message}", ErrorBrush);
            }
        }

        private async Task ExportLogsCsvAsync()
        {
            if (Logs.Count == 0)
            {
                SetMessage("No logs to export.", ErrorBrush);
                return;
            }

            try
            {
                var fileName = $"AttendanceLogs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var dialog = new SaveFileDialog
                {
                    Title = "Export Attendance Logs",
                    FileName = fileName,
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                var result = dialog.ShowDialog();
                if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    SetMessage("Attendance export canceled.", InfoBrush);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("EmpNo,Employee,Date,Time,Type,Source,Device");
                foreach (var row in Logs)
                {
                    sb.AppendLine(string.Join(",",
                        CsvLog(row.EmployeeNo),
                        CsvLog(row.EmployeeName),
                        CsvLog(row.LogDateText),
                        CsvLog(row.LogTimeText),
                        CsvLog(row.LogType),
                        CsvLog(row.Source),
                        CsvLog(row.DeviceName)));
                }

                await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
                SetMessage($"Logs exported: {dialog.FileName}", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Export failed: {ex.Message}", ErrorBrush);
            }
        }

        private static string CsvLog(string? input)
        {
            var value = input ?? string.Empty;
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        private static int FindHeaderIndex(IReadOnlyList<string> headers, params string[] aliases)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                var key = headers[i].Trim().ToLowerInvariant().Replace(" ", "_");
                foreach (var alias in aliases)
                {
                    if (key == alias.Trim().ToLowerInvariant().Replace(" ", "_"))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string GetField(IReadOnlyList<string> fields, int index)
        {
            if (index < 0 || index >= fields.Count)
            {
                return string.Empty;
            }

            return fields[index];
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
            {
                return result;
            }

            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            result.Add(current.ToString());
            return result;
        }
    }
}

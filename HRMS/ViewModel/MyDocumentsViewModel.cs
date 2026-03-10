using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class MyDocumentsViewModel : INotifyPropertyChanged
    {
        private readonly EmployeeSelfService _dataService = new(DbConfig.ConnectionString);
        private readonly ObservableCollection<MyDocumentRowVm> _allDocuments = new();
        private int _currentUserId;
        private int? _currentEmployeeId;
        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _selectedType = "All";
        private string _statusMessage = "Ready.";
        private Brush _statusBrush = Brushes.SeaGreen;

        public MyDocumentsViewModel()
        {
            TypeOptions.Add("All");
            TypeOptions.Add("Payslip");
            TypeOptions.Add("Leave Attachment");
            TypeOptions.Add("Training Certificate");

            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            OpenDocumentCommand = new AsyncRelayCommand(OpenDocumentAsync);
        }

        public ObservableCollection<string> TypeOptions { get; } = new();
        public ObservableCollection<MyDocumentRowVm> Documents { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand OpenDocumentCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetField(ref _selectedType, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetField(ref _statusBrush, value);
        }

        public event EventHandler<string>? OpenModuleRequested;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            _currentUserId = user?.UserId ?? 0;
            _currentEmployeeId = user?.EmployeeId;
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                if ((!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0) && _currentUserId > 0)
                {
                    _currentEmployeeId = await _dataService.GetEmployeeIdByUserIdAsync(_currentUserId);
                }

                if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
                {
                    _allDocuments.Clear();
                    Documents.Clear();
                    SetMessage("Employee profile is not linked to this account.", Brushes.IndianRed);
                    return;
                }

                var data = await _dataService.GetEmployeeDocumentsAsync(_currentEmployeeId.Value, 400);
                RebuildRows(data);
                ApplyFilter();
                SetMessage($"Loaded {Documents.Count} document(s).", Brushes.SeaGreen);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to load documents: {ex.Message}", Brushes.IndianRed);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OpenDocumentAsync(object? parameter)
        {
            if (parameter is not MyDocumentRowVm row)
            {
                return;
            }

            await Task.Yield();

            try
            {
                if (!string.IsNullOrWhiteSpace(row.FilePath))
                {
                    var fullPath = row.FilePath!;
                    if (!Path.IsPathRooted(fullPath))
                    {
                        fullPath = Path.GetFullPath(fullPath);
                    }

                    if (!File.Exists(fullPath))
                    {
                        SetMessage($"File not found: {fullPath}", Brushes.IndianRed);
                        return;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                    SetMessage("Document opened.", Brushes.SeaGreen);
                    return;
                }

                OpenModuleRequested?.Invoke(this, row.ModuleKey);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to open document: {ex.Message}", Brushes.IndianRed);
            }
        }

        private void RebuildRows(IReadOnlyList<EmployeeDocumentDto> data)
        {
            _allDocuments.Clear();
            foreach (var item in data)
            {
                _allDocuments.Add(new MyDocumentRowVm(item));
            }
        }

        private void ApplyFilter()
        {
            var query = (SearchText ?? string.Empty).Trim();
            var filterType = (SelectedType ?? "All").Trim();

            var source = _allDocuments.AsEnumerable();

            if (!string.Equals(filterType, "All", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(x => string.Equals(x.DocumentType, filterType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                source = source.Where(x =>
                    ContainsText(x.DocumentType, query) ||
                    ContainsText(x.Title, query) ||
                    ContainsText(x.Details, query) ||
                    ContainsText(x.Status, query) ||
                    ContainsText(x.SourceModuleLabel, query));
            }

            Documents.Clear();
            foreach (var item in source)
            {
                Documents.Add(item);
            }
        }

        private static bool ContainsText(string? text, string query) =>
            !string.IsNullOrWhiteSpace(text) &&
            text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void SetMessage(string message, Brush brush)
        {
            StatusMessage = message;
            StatusBrush = brush;
        }

        private bool SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class MyDocumentRowVm
    {
        public MyDocumentRowVm(EmployeeDocumentDto dto)
        {
            SourceId = dto.SourceId;
            DocumentType = dto.DocumentType;
            Title = dto.Title;
            Details = dto.Details;
            Status = dto.Status;
            EventAt = dto.EventAt;
            EventAtText = dto.EventAt == DateTime.MinValue
                ? "-"
                : dto.EventAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
            ModuleKey = string.IsNullOrWhiteSpace(dto.ModuleKey) ? "DASHBOARD" : dto.ModuleKey.Trim().ToUpperInvariant();
            SourceModuleLabel = ModuleKey switch
            {
                "PAYROLL" => "Payroll",
                "LEAVE" => "Leave",
                "DEVELOPMENT" => "Development",
                _ => "Dashboard"
            };
            FilePath = dto.FilePath;
            ActionLabel = string.IsNullOrWhiteSpace(FilePath) ? "Open Source" : "Open File";
        }

        public long SourceId { get; }
        public string DocumentType { get; }
        public string Title { get; }
        public string Details { get; }
        public string Status { get; }
        public DateTime EventAt { get; }
        public string EventAtText { get; }
        public string ModuleKey { get; }
        public string SourceModuleLabel { get; }
        public string? FilePath { get; }
        public string ActionLabel { get; }
    }
}

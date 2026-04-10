using HRMS.Model;
using Microsoft.Win32;
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
    public class DocumentsViewModel : INotifyPropertyChanged
    {
        private readonly EmployeeSelfService _dataService = new(DbConfig.ConnectionString);
        private readonly DocumentChecklistDataService _checklistService = new(DbConfig.ConnectionString);
        private readonly ObservableCollection<MyDocumentRowVm> _allDocuments = new();
        private readonly ObservableCollection<EmployeeChecklistDocumentRowVm> _allChecklistDocuments = new();
        private int _currentUserId;
        private int? _currentEmployeeId;
        private bool _isAdminOrHr;
        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _selectedType = "All";
        private string _selectedChecklistFilePath = string.Empty;
        private string _statusMessage = "Ready.";
        private Brush _statusBrush = Brushes.SeaGreen;
        private EmployeeChecklistDocumentRowVm? _selectedChecklistDocument;

        public DocumentsViewModel()
        {
            TypeOptions.Add("All");
            TypeOptions.Add("Payslip");
            TypeOptions.Add("Leave Attachment");
            TypeOptions.Add("Training Certificate");

            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            OpenDocumentCommand = new AsyncRelayCommand(OpenDocumentAsync);
            BrowseChecklistFileCommand = new AsyncRelayCommand(_ => BrowseChecklistFileAsync());
            UploadChecklistFileCommand = new AsyncRelayCommand(_ => UploadChecklistFileAsync());
            OpenChecklistAttachmentCommand = new AsyncRelayCommand(OpenChecklistAttachmentAsync);
        }

        public ObservableCollection<string> TypeOptions { get; } = new();
        public ObservableCollection<MyDocumentRowVm> Documents { get; } = new();
        public ObservableCollection<EmployeeChecklistDocumentRowVm> ChecklistDocuments { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand OpenDocumentCommand { get; }
        public ICommand BrowseChecklistFileCommand { get; }
        public ICommand UploadChecklistFileCommand { get; }
        public ICommand OpenChecklistAttachmentCommand { get; }

        public string DocumentsTitle => _isAdminOrHr ? "Documents" : "My Documents";

        public string DocumentsSubtitle => _isAdminOrHr
            ? "Central access to employee payslips, leave attachments, and training certificates."
            : "Central access for required document submission, payslips, leave attachments, and training certificates.";

        public bool ShowChecklistSubmission => !_isAdminOrHr;
        public bool HasLinkedEmployee => _currentEmployeeId.HasValue && _currentEmployeeId.Value > 0;
        public string ChecklistTitle => "Required Documents Submission";
        public string ChecklistSubtitle => HasLinkedEmployee
            ? "Select a requirement, browse a file, then submit it for verifier review."
            : "Your account must be linked to an employee profile before you can submit required documents.";
        public string SelectedChecklistDocumentTitle => SelectedChecklistDocument == null
            ? "Select a required document"
            : $"{SelectedChecklistDocument.DocumentName} ({SelectedChecklistDocument.TierLabel})";
        public bool CanUploadChecklist =>
            !IsLoading &&
            HasLinkedEmployee &&
            SelectedChecklistDocument != null &&
            !string.IsNullOrWhiteSpace(SelectedChecklistFilePath);
        public bool CanOpenChecklistAttachment => SelectedChecklistDocument?.HasAttachment == true;

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetField(ref _isLoading, value))
                {
                    NotifyChecklistStateChanged();
                }
            }
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

        public EmployeeChecklistDocumentRowVm? SelectedChecklistDocument
        {
            get => _selectedChecklistDocument;
            set
            {
                if (SetField(ref _selectedChecklistDocument, value))
                {
                    NotifyChecklistStateChanged();
                }
            }
        }

        public string SelectedChecklistFilePath
        {
            get => _selectedChecklistFilePath;
            set
            {
                if (SetField(ref _selectedChecklistFilePath, value))
                {
                    NotifyChecklistStateChanged();
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
            _isAdminOrHr = IsAdminOrHrRole(user?.RoleName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentsTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentsSubtitle)));
            NotifyChecklistStateChanged();
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                var selectedChecklistId = SelectedChecklistDocument?.ChecklistId;

                if (_isAdminOrHr)
                {
                    ClearChecklistRows();
                    var allData = await _dataService.GetAllEmployeeDocumentsAsync(800);
                    RebuildRows(allData);
                    ApplyFilter();
                    SetMessage($"Loaded {Documents.Count} document(s).", Brushes.SeaGreen);
                    return;
                }

                if ((!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0) && _currentUserId > 0)
                {
                    _currentEmployeeId = await _dataService.GetEmployeeIdByUserIdAsync(_currentUserId);
                }

                if (!_currentEmployeeId.HasValue || _currentEmployeeId.Value <= 0)
                {
                    ClearChecklistRows();
                    _allDocuments.Clear();
                    Documents.Clear();
                    SetMessage("Employee profile is not linked to this account.", Brushes.IndianRed);
                    return;
                }

                await _checklistService.GenerateChecklistForEmployeeAsync(_currentEmployeeId.Value);

                var checklistTask = _checklistService.GetChecklistAsync(_currentEmployeeId.Value);
                var dataTask = _dataService.GetEmployeeDocumentsAsync(_currentEmployeeId.Value, 400);

                await Task.WhenAll(checklistTask, dataTask);

                RebuildChecklistRows(checklistTask.Result, selectedChecklistId);

                var data = dataTask.Result;
                RebuildRows(data);
                ApplyFilter();
                SetMessage(
                    $"Loaded {ChecklistDocuments.Count} required document(s) and {Documents.Count} related record(s).",
                    Brushes.SeaGreen);
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
                if (string.Equals(row.ModuleKey, "LEAVE", StringComparison.OrdinalIgnoreCase) && row.SourceId > 0)
                {
                    var attachment = await _dataService.GetLeaveAttachmentFileAsync(row.SourceId);
                    if (attachment.HasValue)
                    {
                        var extractedPath = WriteLeaveAttachmentToLocalCache(
                            row.SourceId,
                            attachment.Value.FileName,
                            attachment.Value.FileBlob);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = extractedPath,
                            UseShellExecute = true
                        });

                        SetMessage("Document opened.", Brushes.SeaGreen);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(row.FilePath))
                {
                    var fullPath = row.FilePath!;
                    if (!Path.IsPathRooted(fullPath))
                    {
                        fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, fullPath));
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

        private Task BrowseChecklistFileAsync()
        {
            if (!HasLinkedEmployee)
            {
                SetMessage("Employee profile is not linked to this account.", Brushes.IndianRed);
                return Task.CompletedTask;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Required Document",
                Filter = "All files (*.*)|*.*"
            };

            var result = dialog.ShowDialog();
            if (result == true && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                SelectedChecklistFilePath = dialog.FileName;
                SetMessage("Required document selected.", Brushes.SeaGreen);
            }

            return Task.CompletedTask;
        }

        private async Task UploadChecklistFileAsync()
        {
            if (!HasLinkedEmployee)
            {
                SetMessage("Employee profile is not linked to this account.", Brushes.IndianRed);
                return;
            }

            if (SelectedChecklistDocument == null)
            {
                SetMessage("Select a required document first.", Brushes.IndianRed);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedChecklistFilePath))
            {
                SetMessage("Browse and select a file first.", Brushes.IndianRed);
                return;
            }

            try
            {
                await _checklistService.AddChecklistAttachmentAsync(
                    SelectedChecklistDocument.ChecklistId,
                    _currentEmployeeId!.Value,
                    SelectedChecklistFilePath,
                    _currentEmployeeId.Value);

                var submittedCode = SelectedChecklistDocument.DocumentCode;
                SelectedChecklistFilePath = string.Empty;
                await RefreshAsync();
                SetMessage($"{submittedCode} submitted for verifier review.", Brushes.SeaGreen);
                SystemRefreshBus.Raise("ChecklistDocumentSubmitted");
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to submit required document: {ex.Message}", Brushes.IndianRed);
            }
        }

        private async Task OpenChecklistAttachmentAsync(object? parameter)
        {
            var row = parameter as EmployeeChecklistDocumentRowVm ?? SelectedChecklistDocument;
            if (row == null)
            {
                SetMessage("Select a required document first.", Brushes.IndianRed);
                return;
            }

            try
            {
                var attachment = await _checklistService.GetChecklistAttachmentAsync(row.ChecklistId, _currentEmployeeId);
                if (attachment == null)
                {
                    SetMessage("No uploaded file is available for this requirement.", Brushes.IndianRed);
                    return;
                }

                if (TryOpenExistingFile(attachment.FilePath))
                {
                    SetMessage("Uploaded file opened.", Brushes.SeaGreen);
                    return;
                }

                if (attachment.FileBlob != null && attachment.FileBlob.Length > 0)
                {
                    var extractedPath = WriteChecklistAttachmentToLocalCache(row.ChecklistId, attachment.FileName, attachment.FileBlob);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = extractedPath,
                        UseShellExecute = true
                    });
                    SetMessage("Uploaded file opened.", Brushes.SeaGreen);
                    return;
                }

                SetMessage("Uploaded file is no longer accessible.", Brushes.IndianRed);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to open uploaded file: {ex.Message}", Brushes.IndianRed);
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

        private void RebuildChecklistRows(IReadOnlyList<DocumentChecklistItemDto> checklist, int? preferredChecklistId)
        {
            _allChecklistDocuments.Clear();
            foreach (var item in checklist)
            {
                _allChecklistDocuments.Add(new EmployeeChecklistDocumentRowVm(item));
            }

            ChecklistDocuments.Clear();
            foreach (var item in _allChecklistDocuments)
            {
                ChecklistDocuments.Add(item);
            }

            SelectedChecklistDocument = preferredChecklistId.HasValue
                ? ChecklistDocuments.FirstOrDefault(x => x.ChecklistId == preferredChecklistId.Value)
                : ChecklistDocuments.FirstOrDefault();
        }

        private void ClearChecklistRows()
        {
            _allChecklistDocuments.Clear();
            ChecklistDocuments.Clear();
            SelectedChecklistDocument = null;
            SelectedChecklistFilePath = string.Empty;
            NotifyChecklistStateChanged();
        }

        private static string WriteLeaveAttachmentToLocalCache(long sourceId, string fileName, byte[] fileBlob)
        {
            var baseName = string.IsNullOrWhiteSpace(fileName)
                ? $"leave_attachment_{sourceId}.bin"
                : fileName.Trim();
            var sanitized = string.Concat(baseName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HRMS",
                "leave_attachments_cache");
            Directory.CreateDirectory(folder);

            var targetPath = Path.Combine(folder, $"{sourceId}_{sanitized}");
            File.WriteAllBytes(targetPath, fileBlob);
            return targetPath;
        }

        private static string WriteChecklistAttachmentToLocalCache(int checklistId, string fileName, byte[] fileBlob)
        {
            var baseName = string.IsNullOrWhiteSpace(fileName)
                ? $"checklist_{checklistId}.bin"
                : fileName.Trim();
            var sanitized = string.Concat(baseName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HRMS",
                "checklist_attachments_cache");
            Directory.CreateDirectory(folder);

            var targetPath = Path.Combine(folder, $"{checklistId}_{sanitized}");
            File.WriteAllBytes(targetPath, fileBlob);
            return targetPath;
        }

        private static bool TryOpenExistingFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var fullPath = filePath.Trim();
            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, fullPath));
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
            return true;
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

        private static bool IsAdminOrHrRole(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var normalized = roleName.Trim();
            return string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "HR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "HR Manager", StringComparison.OrdinalIgnoreCase);
        }

        private void SetMessage(string message, Brush brush)
        {
            StatusMessage = message;
            StatusBrush = brush;
        }

        private void NotifyChecklistStateChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowChecklistSubmission)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLinkedEmployee)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChecklistTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChecklistSubtitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedChecklistDocumentTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanUploadChecklist)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanOpenChecklistAttachment)));
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

    public class EmployeeChecklistDocumentRowVm
    {
        public EmployeeChecklistDocumentRowVm(DocumentChecklistItemDto dto)
        {
            ChecklistId = dto.ChecklistId;
            DocumentCode = string.IsNullOrWhiteSpace(dto.DocumentCode) ? "-" : dto.DocumentCode.Trim();
            DocumentName = string.IsNullOrWhiteSpace(dto.DocumentName) ? "Required Document" : dto.DocumentName.Trim();
            DocumentTier = dto.DocumentTier;
            TierLabel = $"Tier {dto.DocumentTier}";
            StatusKey = string.IsNullOrWhiteSpace(dto.Status) ? "not_submitted" : dto.Status.Trim().ToLowerInvariant();
            SubmittedDate = dto.SubmittedDate;
            UploadedAt = dto.UploadedAt;
            FileName = dto.FileName;
            FilePath = dto.FilePath;
            FileSize = dto.FileSize;
        }

        public int ChecklistId { get; }
        public string DocumentCode { get; }
        public string DocumentName { get; }
        public int DocumentTier { get; }
        public string TierLabel { get; }
        public string StatusKey { get; }
        public DateTime? SubmittedDate { get; }
        public DateTime? UploadedAt { get; }
        public string? FileName { get; }
        public string? FilePath { get; }
        public long FileSize { get; }
        public bool HasAttachment => !string.IsNullOrWhiteSpace(FileName) || !string.IsNullOrWhiteSpace(FilePath);
        public string StatusLabel => StatusKey switch
        {
            "not_submitted" => "Not Submitted",
            "submitted" => "Submitted",
            "verified" => "Verified",
            "expired" => "Expired",
            "waived" => "Waived",
            _ => StatusKey
        };
        public string SubmittedDateText => SubmittedDate.HasValue ? SubmittedDate.Value.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) : "-";
        public string UploadedAtText => UploadedAt.HasValue ? UploadedAt.Value.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture) : "No upload yet";
        public string AttachmentText => HasAttachment ? "Available" : "None";
    }
}

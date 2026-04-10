using HRMS.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class DocumentVerificationViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush WarningBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9B53B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly DocumentChecklistDataService _dataService = new(DbConfig.ConnectionString);

        private bool _isBusy;
        private bool _suppressSelectedEmployeeLoad;
        private string _searchText = string.Empty;
        private string _actionMessage = "Ready.";
        private Brush _actionMessageBrush = InfoBrush;
        private int _totalEmployees;
        private int _completeEmployees;
        private int _partialEmployees;
        private int _incompleteEmployees;
        private int _expiringSoonDocuments;
        private string _currentVerifierDisplayName = "HR";

        private DocumentChecklistEmployeeSummaryDto? _selectedEmployee;
        private DocumentChecklistItemVm? _selectedDocument;
        private string _selectedDocumentStatus = "not_submitted";
        private DateTime? _selectedSubmittedDate;
        private DateTime? _selectedExpiryDate;
        private DateTime? _selectedVerifiedDate;
        private string _selectedWaivedReason = string.Empty;
        private string _selectedRemarks = string.Empty;

        public ObservableCollection<DocumentChecklistEmployeeSummaryDto> Employees { get; } = new();
        public ObservableCollection<DocumentChecklistItemVm> Tier1Documents { get; } = new();
        public ObservableCollection<DocumentChecklistItemVm> Tier2Documents { get; } = new();
        public ObservableCollection<DocumentChecklistItemVm> Tier3Documents { get; } = new();
        public ObservableCollection<DocumentChecklistItemVm> Tier4Documents { get; } = new();
        public ObservableCollection<ExpiringDocumentDto> ExpiringDocuments { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new()
        {
            "not_submitted",
            "submitted",
            "verified",
            "expired",
            "waived"
        };

        public ICommand RefreshCommand { get; }
        public ICommand SaveDocumentCommand { get; }
        public ICommand OpenAttachmentCommand { get; }

        public DocumentVerificationViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
            SaveDocumentCommand = new AsyncRelayCommand(_ => SaveSelectedDocumentAsync());
            OpenAttachmentCommand = new AsyncRelayCommand(OpenSelectedAttachmentAsync);
            _ = LoadAsync();
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveDocument));
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        public string ActionMessage
        {
            get => _actionMessage;
            private set
            {
                if (_actionMessage == value)
                {
                    return;
                }

                _actionMessage = value;
                OnPropertyChanged();
            }
        }

        public Brush ActionMessageBrush
        {
            get => _actionMessageBrush;
            private set
            {
                if (Equals(_actionMessageBrush, value))
                {
                    return;
                }

                _actionMessageBrush = value;
                OnPropertyChanged();
            }
        }

        public int TotalEmployees
        {
            get => _totalEmployees;
            private set
            {
                if (_totalEmployees == value)
                {
                    return;
                }

                _totalEmployees = value;
                OnPropertyChanged();
            }
        }

        public int CompleteEmployees
        {
            get => _completeEmployees;
            private set
            {
                if (_completeEmployees == value)
                {
                    return;
                }

                _completeEmployees = value;
                OnPropertyChanged();
            }
        }

        public int PartialEmployees
        {
            get => _partialEmployees;
            private set
            {
                if (_partialEmployees == value)
                {
                    return;
                }

                _partialEmployees = value;
                OnPropertyChanged();
            }
        }

        public int IncompleteEmployees
        {
            get => _incompleteEmployees;
            private set
            {
                if (_incompleteEmployees == value)
                {
                    return;
                }

                _incompleteEmployees = value;
                OnPropertyChanged();
            }
        }

        public int ExpiringSoonDocuments
        {
            get => _expiringSoonDocuments;
            private set
            {
                if (_expiringSoonDocuments == value)
                {
                    return;
                }

                _expiringSoonDocuments = value;
                OnPropertyChanged();
            }
        }

        public DocumentChecklistEmployeeSummaryDto? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (_selectedEmployee == value)
                {
                    return;
                }

                _selectedEmployee = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedEmployee));
                OnPropertyChanged(nameof(SelectedEmployeeTitle));
                OnPropertyChanged(nameof(SelectedEmployeeSubtitle));

                if (!_suppressSelectedEmployeeLoad)
                {
                    _ = LoadSelectedEmployeeChecklistAsync();
                }
            }
        }

        public DocumentChecklistItemVm? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (_selectedDocument == value)
                {
                    return;
                }

                _selectedDocument = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedDocument));
                OnPropertyChanged(nameof(SelectedDocumentTitle));
                OnPropertyChanged(nameof(SelectedDocumentHasAttachment));
                OnPropertyChanged(nameof(SelectedDocumentAttachmentText));
                OnPropertyChanged(nameof(CanSaveDocument));
                ApplySelectedDocumentState();
            }
        }

        public string SelectedDocumentStatus
        {
            get => _selectedDocumentStatus;
            set
            {
                if (_selectedDocumentStatus == value)
                {
                    return;
                }

                _selectedDocumentStatus = value ?? "not_submitted";
                OnPropertyChanged();
            }
        }

        public DateTime? SelectedSubmittedDate
        {
            get => _selectedSubmittedDate;
            set
            {
                if (_selectedSubmittedDate == value)
                {
                    return;
                }

                _selectedSubmittedDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime? SelectedExpiryDate
        {
            get => _selectedExpiryDate;
            set
            {
                if (_selectedExpiryDate == value)
                {
                    return;
                }

                _selectedExpiryDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime? SelectedVerifiedDate
        {
            get => _selectedVerifiedDate;
            set
            {
                if (_selectedVerifiedDate == value)
                {
                    return;
                }

                _selectedVerifiedDate = value;
                OnPropertyChanged();
            }
        }

        public string SelectedWaivedReason
        {
            get => _selectedWaivedReason;
            set
            {
                if (_selectedWaivedReason == value)
                {
                    return;
                }

                _selectedWaivedReason = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string SelectedRemarks
        {
            get => _selectedRemarks;
            set
            {
                if (_selectedRemarks == value)
                {
                    return;
                }

                _selectedRemarks = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public bool HasSelectedEmployee => SelectedEmployee != null;
        public bool HasSelectedDocument => SelectedDocument != null;
        public bool SelectedDocumentHasAttachment => SelectedDocument?.HasAttachment == true;
        public bool CanSaveDocument => !IsBusy && SelectedDocument != null;
        public string SelectedEmployeeTitle => SelectedEmployee == null ? "Select an employee" : SelectedEmployee.EmployeeName;
        public string SelectedEmployeeSubtitle => SelectedEmployee == null
            ? "Choose an employee to review their generated document checklist."
            : $"{SelectedEmployee.PositionName} | {SelectedEmployee.EmploymentType} | {SelectedEmployee.ProgressText} complete";
        public string SelectedDocumentTitle => SelectedDocument == null
            ? "Select a document"
            : $"{SelectedDocument.DocumentName} ({SelectedDocument.TierLabel})";
        public string SelectedDocumentAttachmentText => SelectedDocument == null
            ? "Select a document to review its uploaded file."
            : SelectedDocument.HasAttachment
                ? $"{SelectedDocument.FileName ?? "Uploaded file"} | {SelectedDocument.UploadedAtText}"
                : "No employee file uploaded yet.";

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            _currentVerifierDisplayName = string.IsNullOrWhiteSpace(user?.FullName)
                ? string.IsNullOrWhiteSpace(user?.Username) ? "HR" : user!.Username.Trim()
                : user!.FullName.Trim();
        }

        public Task RefreshAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            SetMessage("Loading document verification data...", InfoBrush);

            var selectedEmployeeId = SelectedEmployee?.EmployeeId;
            var selectedChecklistId = SelectedDocument?.ChecklistId;

            try
            {
                await _dataService.EnsureChecklistsForAllEmployeesAsync();

                var stats = await _dataService.GetChecklistSummaryStatsAsync();
                var employees = await _dataService.GetEmployeeChecklistSummariesAsync(SearchText, 500);
                var expiringDocs = await _dataService.GetExpiringDocumentsAsync(30);

                TotalEmployees = stats.TotalEmployees;
                CompleteEmployees = stats.CompleteEmployees;
                PartialEmployees = stats.PartialEmployees;
                IncompleteEmployees = stats.IncompleteEmployees;
                ExpiringSoonDocuments = stats.ExpiringSoonDocuments;

                Employees.Clear();
                foreach (var employee in employees)
                {
                    Employees.Add(employee);
                }

                ExpiringDocuments.Clear();
                foreach (var expiringDocument in expiringDocs)
                {
                    ExpiringDocuments.Add(expiringDocument);
                }

                var restoredEmployee = selectedEmployeeId.HasValue
                    ? Employees.FirstOrDefault(x => x.EmployeeId == selectedEmployeeId.Value)
                    : Employees.FirstOrDefault();

                _suppressSelectedEmployeeLoad = true;
                SelectedEmployee = restoredEmployee;
                _suppressSelectedEmployeeLoad = false;

                await LoadSelectedEmployeeChecklistAsync(selectedChecklistId);

                SetMessage($"Loaded {Employees.Count} employee checklists and {ExpiringDocuments.Count} expiring documents.", SuccessBrush);
            }
            catch (Exception ex)
            {
                ClearChecklistCollections();
                SelectedDocument = null;
                SetMessage($"Unable to load document verifier data: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSelectedEmployeeChecklistAsync(int? preferredChecklistId = null)
        {
            if (SelectedEmployee == null)
            {
                ClearChecklistCollections();
                SelectedDocument = null;
                return;
            }

            try
            {
                var checklist = await _dataService.GetChecklistAsync(SelectedEmployee.EmployeeId);
                RebuildChecklistCollections(checklist);

                var preferredDocument = preferredChecklistId.HasValue
                    ? Tier1Documents.Concat(Tier2Documents).Concat(Tier3Documents).Concat(Tier4Documents)
                        .FirstOrDefault(x => x.ChecklistId == preferredChecklistId.Value)
                    : Tier1Documents.Concat(Tier2Documents).Concat(Tier3Documents).Concat(Tier4Documents).FirstOrDefault();

                SelectedDocument = preferredDocument;
            }
            catch (Exception ex)
            {
                ClearChecklistCollections();
                SelectedDocument = null;
                SetMessage($"Unable to load checklist documents: {ex.Message}", ErrorBrush);
            }
        }

        private async Task SaveSelectedDocumentAsync()
        {
            if (SelectedDocument == null)
            {
                SetMessage("Select a document before saving.", WarningBrush);
                return;
            }

            if (string.Equals(SelectedDocumentStatus, "waived", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(SelectedWaivedReason))
            {
                SetMessage("Waived documents require a waived reason.", WarningBrush);
                return;
            }

            try
            {
                IsBusy = true;
                await _dataService.UpdateDocumentStatusAsync(
                    SelectedDocument.ChecklistId,
                    SelectedDocumentStatus,
                    SelectedSubmittedDate,
                    SelectedExpiryDate,
                    SelectedVerifiedDate,
                    string.Equals(SelectedDocumentStatus, "verified", StringComparison.OrdinalIgnoreCase)
                        ? _currentVerifierDisplayName
                        : null,
                    SelectedWaivedReason,
                    SelectedRemarks);

                IsBusy = false;
                SetMessage($"Saved {SelectedDocument.DocumentCode} for {SelectedEmployee?.EmployeeName ?? "employee"}.", SuccessBrush);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to save document status: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenSelectedAttachmentAsync(object? parameter)
        {
            var document = parameter as DocumentChecklistItemVm ?? SelectedDocument;
            if (document == null)
            {
                SetMessage("Select a document first.", WarningBrush);
                return;
            }

            try
            {
                var attachment = await _dataService.GetChecklistAttachmentAsync(document.ChecklistId);
                if (attachment == null)
                {
                    SetMessage("No uploaded file is available for this document.", WarningBrush);
                    return;
                }

                var opened = TryOpenExistingFile(attachment.FilePath);
                if (!string.IsNullOrWhiteSpace(opened))
                {
                    SetMessage("Uploaded file opened.", SuccessBrush);
                    return;
                }

                if (attachment.FileBlob != null && attachment.FileBlob.Length > 0)
                {
                    var extractedPath = WriteChecklistAttachmentToLocalCache(document.ChecklistId, attachment.FileName, attachment.FileBlob);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = extractedPath,
                        UseShellExecute = true
                    });
                    SetMessage("Uploaded file opened.", SuccessBrush);
                    return;
                }

                SetMessage("Uploaded file is no longer accessible.", ErrorBrush);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to open uploaded file: {ex.Message}", ErrorBrush);
            }
        }

        private void RebuildChecklistCollections(System.Collections.Generic.IReadOnlyList<DocumentChecklistItemDto> checklist)
        {
            Tier1Documents.Clear();
            Tier2Documents.Clear();
            Tier3Documents.Clear();
            Tier4Documents.Clear();

            foreach (var item in checklist.Select(DocumentChecklistItemVm.FromDto))
            {
                switch (item.DocumentTier)
                {
                    case 1:
                        Tier1Documents.Add(item);
                        break;
                    case 2:
                        Tier2Documents.Add(item);
                        break;
                    case 3:
                        Tier3Documents.Add(item);
                        break;
                    case 4:
                        Tier4Documents.Add(item);
                        break;
                    default:
                        Tier1Documents.Add(item);
                        break;
                }
            }
        }

        private void ClearChecklistCollections()
        {
            Tier1Documents.Clear();
            Tier2Documents.Clear();
            Tier3Documents.Clear();
            Tier4Documents.Clear();
        }

        private void ApplySelectedDocumentState()
        {
            if (SelectedDocument == null)
            {
                SelectedDocumentStatus = "not_submitted";
                SelectedSubmittedDate = null;
                SelectedExpiryDate = null;
                SelectedVerifiedDate = null;
                SelectedWaivedReason = string.Empty;
                SelectedRemarks = string.Empty;
                return;
            }

            SelectedDocumentStatus = SelectedDocument.StatusKey;
            SelectedSubmittedDate = SelectedDocument.SubmittedDate;
            SelectedExpiryDate = SelectedDocument.ExpiryDate;
            SelectedVerifiedDate = SelectedDocument.VerifiedDate;
            SelectedWaivedReason = SelectedDocument.WaivedReason ?? string.Empty;
            SelectedRemarks = SelectedDocument.Remarks ?? string.Empty;
        }

        private void SetMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        private static string? TryOpenExistingFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var candidate = filePath.Trim();
            if (!Path.IsPathRooted(candidate))
            {
                candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, candidate));
            }

            if (!File.Exists(candidate))
            {
                return null;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = candidate,
                UseShellExecute = true
            });
            return candidate;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class DocumentChecklistItemVm
    {
        public int ChecklistId { get; init; }
        public int EmployeeId { get; init; }
        public string PositionName { get; init; } = string.Empty;
        public string EmploymentType { get; init; } = string.Empty;
        public string DocumentCode { get; init; } = string.Empty;
        public string DocumentName { get; init; } = string.Empty;
        public int DocumentTier { get; init; }
        public bool IsRequired { get; init; }
        public string StatusKey { get; init; } = "not_submitted";
        public DateTime? SubmittedDate { get; init; }
        public DateTime? ExpiryDate { get; init; }
        public DateTime? VerifiedDate { get; init; }
        public string? VerifiedBy { get; init; }
        public string? WaivedReason { get; init; }
        public string? Remarks { get; init; }
        public string? FileName { get; init; }
        public string? FilePath { get; init; }
        public long FileSize { get; init; }
        public DateTime? UploadedAt { get; init; }
        public string TierLabel => $"Tier {DocumentTier}";
        public bool HasAttachment => !string.IsNullOrWhiteSpace(FilePath) || !string.IsNullOrWhiteSpace(FileName);
        public string UploadedAtText => UploadedAt.HasValue ? $"Uploaded {UploadedAt.Value:MMM dd, yyyy hh:mm tt}" : "No upload yet";
        public string StatusLabel => StatusKey switch
        {
            "not_submitted" => "Not Submitted",
            "submitted" => "Submitted",
            "verified" => "Verified",
            "expired" => "Expired",
            "waived" => "Waived",
            _ => StatusKey
        };

        public static DocumentChecklistItemVm FromDto(DocumentChecklistItemDto item) => new()
        {
            ChecklistId = item.ChecklistId,
            EmployeeId = item.EmployeeId,
            PositionName = item.PositionName,
            EmploymentType = item.EmploymentType,
            DocumentCode = item.DocumentCode,
            DocumentName = item.DocumentName,
            DocumentTier = item.DocumentTier,
            IsRequired = item.IsRequired,
            StatusKey = item.Status,
            SubmittedDate = item.SubmittedDate,
            ExpiryDate = item.ExpiryDate,
            VerifiedDate = item.VerifiedDate,
            VerifiedBy = item.VerifiedBy,
            WaivedReason = item.WaivedReason,
            Remarks = item.Remarks,
            FileName = item.FileName,
            FilePath = item.FilePath,
            FileSize = item.FileSize,
            UploadedAt = item.UploadedAt
        };
    }
}

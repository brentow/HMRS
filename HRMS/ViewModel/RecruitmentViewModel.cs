using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.ViewModel
{
    public class RecruitmentViewModel : INotifyPropertyChanged
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5B6C"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E9D5B"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D84343"));

        private readonly RecruitmentDataService _dataService = new(DbConfig.ConnectionString);

        private readonly List<JobPostingRowVm> _allPostings = new();
        private readonly List<ApplicantRowVm> _allApplicants = new();
        private readonly List<ApplicationRowVm> _allApplications = new();
        private readonly List<InterviewRowVm> _allInterviews = new();
        private readonly List<OfferRowVm> _allOffers = new();
        private readonly List<PositionOptionVm> _allPositionOptions = new();

        private bool _isBusy;
        private string _actionMessage = "Ready.";
        private Brush _actionMessageBrush = InfoBrush;

        private int _totalPostings;
        private int _openPostings;
        private int _totalApplicants;
        private int _totalApplications;
        private int _scheduledInterviews;
        private int _pendingOffers;

        private string _postingSearchText = string.Empty;
        private string _selectedPostingStatusFilter = "All";
        private string _applicantSearchText = string.Empty;
        private string _applicationSearchText = string.Empty;
        private string _selectedApplicationStatusFilter = "All";
        private string _interviewSearchText = string.Empty;
        private string _selectedInterviewStatusFilter = "All";
        private string _offerSearchText = string.Empty;
        private string _selectedOfferStatusFilter = "All";

        private string _newPostingCode = string.Empty;
        private string _newPostingTitle = string.Empty;
        private int? _selectedNewDepartmentId;
        private int? _selectedNewPositionId;
        private string _newPostingEmploymentType = "CASUAL";
        private int _newPostingVacancies = 1;
        private DateTime _newPostingOpenDate = DateTime.Today;
        private DateTime? _newPostingCloseDate;
        private string _newPostingStatus = "OPEN";

        private string _newApplicantNo = string.Empty;
        private string _newApplicantFirstName = string.Empty;
        private string _newApplicantLastName = string.Empty;
        private string _newApplicantMiddleName = string.Empty;
        private string _newApplicantEmail = string.Empty;
        private string _newApplicantMobile = string.Empty;
        private string _newApplicantAddress = string.Empty;
        private DateTime? _newApplicantBirthDate;

        private long? _selectedApplicationApplicantId;
        private long? _selectedApplicationPostingId;
        private string _newApplicationStatus = "SUBMITTED";
        private string _newApplicationNotes = string.Empty;

        private long? _selectedInterviewApplicationId;
        private DateTime _newInterviewDate = DateTime.Today;
        private string _newInterviewTimeText = "09:00";
        private string _newInterviewType = "ONSITE";
        private int? _selectedInterviewerEmployeeId;
        private string _newInterviewStatus = "SCHEDULED";
        private string _newInterviewLocation = string.Empty;
        private string _newInterviewRemarks = string.Empty;

        private long? _selectedOfferApplicationId;
        private string _newOfferStatus = "PENDING";
        private string _newOfferSalaryText = string.Empty;
        private DateTime? _newOfferStartDate;
        private string _newOfferRemarks = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<JobPostingRowVm> JobPostings { get; } = new();
        public ObservableCollection<ApplicantRowVm> Applicants { get; } = new();
        public ObservableCollection<ApplicationRowVm> Applications { get; } = new();
        public ObservableCollection<InterviewRowVm> Interviews { get; } = new();
        public ObservableCollection<OfferRowVm> Offers { get; } = new();

        public ObservableCollection<DepartmentOptionVm> DepartmentOptions { get; } = new();
        public ObservableCollection<PositionOptionVm> NewPostingPositionOptions { get; } = new();
        public ObservableCollection<EmployeeOptionVm> EmployeeOptions { get; } = new();
        public ObservableCollection<PostingOptionVm> PostingOptions { get; } = new();
        public ObservableCollection<ApplicantOptionVm> ApplicantOptions { get; } = new();
        public ObservableCollection<ApplicationOptionVm> ApplicationOptions { get; } = new();

        public ObservableCollection<int> VacancyOptions { get; } = new(Enumerable.Range(1, 30));
        public ObservableCollection<string> PostingStatusFilters { get; } = new() { "All", "DRAFT", "OPEN", "CLOSED", "CANCELLED" };
        public ObservableCollection<string> PostingStatuses { get; } = new() { "DRAFT", "OPEN", "CLOSED", "CANCELLED" };
        public ObservableCollection<string> EmploymentTypes { get; } = new() { "PLANTILLA", "CASUAL", "JOB_ORDER", "CONTRACTUAL", "TEMPORARY" };

        public ObservableCollection<string> ApplicationStatusFilters { get; } = new() { "All", "SUBMITTED", "SCREENING", "SHORTLISTED", "INTERVIEW", "OFFERED", "HIRED", "REJECTED", "WITHDRAWN" };
        public ObservableCollection<string> ApplicationStatuses { get; } = new() { "SUBMITTED", "SCREENING", "SHORTLISTED", "INTERVIEW", "OFFERED", "HIRED", "REJECTED", "WITHDRAWN" };

        public ObservableCollection<string> InterviewStatusFilters { get; } = new() { "All", "SCHEDULED", "DONE", "CANCELLED", "NO_SHOW" };
        public ObservableCollection<string> InterviewStatuses { get; } = new() { "SCHEDULED", "DONE", "CANCELLED", "NO_SHOW" };
        public ObservableCollection<string> InterviewTypes { get; } = new() { "ONSITE", "ONLINE", "PHONE" };

        public ObservableCollection<string> OfferStatusFilters { get; } = new() { "All", "PENDING", "ACCEPTED", "DECLINED", "CANCELLED" };
        public ObservableCollection<string> OfferStatuses { get; } = new() { "PENDING", "ACCEPTED", "DECLINED", "CANCELLED" };

        public ICommand RefreshCommand { get; }
        public ICommand AddPostingCommand { get; }
        public ICommand SavePostingCommand { get; }
        public ICommand DeletePostingCommand { get; }
        public ICommand AddApplicantCommand { get; }
        public ICommand SaveApplicantCommand { get; }
        public ICommand DeleteApplicantCommand { get; }
        public ICommand AddApplicationCommand { get; }
        public ICommand SaveApplicationCommand { get; }
        public ICommand DeleteApplicationCommand { get; }
        public ICommand AddInterviewCommand { get; }
        public ICommand SaveInterviewCommand { get; }
        public ICommand DeleteInterviewCommand { get; }
        public ICommand AddOfferCommand { get; }
        public ICommand SaveOfferCommand { get; }
        public ICommand DeleteOfferCommand { get; }

        public RecruitmentViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            AddPostingCommand = new AsyncRelayCommand(_ => AddPostingAsync());
            SavePostingCommand = new AsyncRelayCommand(SavePostingAsync);
            DeletePostingCommand = new AsyncRelayCommand(DeletePostingAsync);

            AddApplicantCommand = new AsyncRelayCommand(_ => AddApplicantAsync());
            SaveApplicantCommand = new AsyncRelayCommand(SaveApplicantAsync);
            DeleteApplicantCommand = new AsyncRelayCommand(DeleteApplicantAsync);

            AddApplicationCommand = new AsyncRelayCommand(_ => AddApplicationAsync());
            SaveApplicationCommand = new AsyncRelayCommand(SaveApplicationAsync);
            DeleteApplicationCommand = new AsyncRelayCommand(DeleteApplicationAsync);

            AddInterviewCommand = new AsyncRelayCommand(_ => AddInterviewAsync());
            SaveInterviewCommand = new AsyncRelayCommand(SaveInterviewAsync);
            DeleteInterviewCommand = new AsyncRelayCommand(DeleteInterviewAsync);

            AddOfferCommand = new AsyncRelayCommand(_ => AddOfferAsync());
            SaveOfferCommand = new AsyncRelayCommand(SaveOfferAsync);
            DeleteOfferCommand = new AsyncRelayCommand(DeleteOfferAsync);

            _ = RefreshAsync();
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
            }
        }

        public int TotalPostings { get => _totalPostings; private set => SetField(ref _totalPostings, value); }
        public int OpenPostings { get => _openPostings; private set => SetField(ref _openPostings, value); }
        public int TotalApplicants { get => _totalApplicants; private set => SetField(ref _totalApplicants, value); }
        public int TotalApplications { get => _totalApplications; private set => SetField(ref _totalApplications, value); }
        public int ScheduledInterviews { get => _scheduledInterviews; private set => SetField(ref _scheduledInterviews, value); }
        public int PendingOffers { get => _pendingOffers; private set => SetField(ref _pendingOffers, value); }

        public string ActionMessage { get => _actionMessage; private set => SetField(ref _actionMessage, value); }
        public Brush ActionMessageBrush { get => _actionMessageBrush; private set => SetField(ref _actionMessageBrush, value); }

        public string PostingSearchText
        {
            get => _postingSearchText;
            set
            {
                if (SetField(ref _postingSearchText, value ?? string.Empty))
                {
                    ApplyPostingFilters();
                }
            }
        }

        public string SelectedPostingStatusFilter
        {
            get => _selectedPostingStatusFilter;
            set
            {
                if (SetField(ref _selectedPostingStatusFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
                {
                    ApplyPostingFilters();
                }
            }
        }

        public string ApplicantSearchText
        {
            get => _applicantSearchText;
            set
            {
                if (SetField(ref _applicantSearchText, value ?? string.Empty))
                {
                    ApplyApplicantFilters();
                }
            }
        }

        public string ApplicationSearchText
        {
            get => _applicationSearchText;
            set
            {
                if (SetField(ref _applicationSearchText, value ?? string.Empty))
                {
                    ApplyApplicationFilters();
                }
            }
        }

        public string SelectedApplicationStatusFilter
        {
            get => _selectedApplicationStatusFilter;
            set
            {
                if (SetField(ref _selectedApplicationStatusFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
                {
                    ApplyApplicationFilters();
                }
            }
        }

        public string InterviewSearchText
        {
            get => _interviewSearchText;
            set
            {
                if (SetField(ref _interviewSearchText, value ?? string.Empty))
                {
                    ApplyInterviewFilters();
                }
            }
        }

        public string SelectedInterviewStatusFilter
        {
            get => _selectedInterviewStatusFilter;
            set
            {
                if (SetField(ref _selectedInterviewStatusFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
                {
                    ApplyInterviewFilters();
                }
            }
        }

        public string OfferSearchText
        {
            get => _offerSearchText;
            set
            {
                if (SetField(ref _offerSearchText, value ?? string.Empty))
                {
                    ApplyOfferFilters();
                }
            }
        }

        public string SelectedOfferStatusFilter
        {
            get => _selectedOfferStatusFilter;
            set
            {
                if (SetField(ref _selectedOfferStatusFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
                {
                    ApplyOfferFilters();
                }
            }
        }

        public string NewPostingCode { get => _newPostingCode; set => SetField(ref _newPostingCode, value ?? string.Empty); }
        public string NewPostingTitle { get => _newPostingTitle; set => SetField(ref _newPostingTitle, value ?? string.Empty); }

        public int? SelectedNewDepartmentId
        {
            get => _selectedNewDepartmentId;
            set
            {
                if (SetField(ref _selectedNewDepartmentId, value))
                {
                    RefreshNewPostingPositionOptions();
                }
            }
        }

        public int? SelectedNewPositionId { get => _selectedNewPositionId; set => SetField(ref _selectedNewPositionId, value); }
        public string NewPostingEmploymentType { get => _newPostingEmploymentType; set => SetField(ref _newPostingEmploymentType, string.IsNullOrWhiteSpace(value) ? "CASUAL" : value); }
        public int NewPostingVacancies { get => _newPostingVacancies; set => SetField(ref _newPostingVacancies, value < 1 ? 1 : value); }
        public DateTime NewPostingOpenDate { get => _newPostingOpenDate; set => SetField(ref _newPostingOpenDate, value.Date); }
        public DateTime? NewPostingCloseDate { get => _newPostingCloseDate; set => SetField(ref _newPostingCloseDate, value?.Date); }
        public string NewPostingStatus { get => _newPostingStatus; set => SetField(ref _newPostingStatus, string.IsNullOrWhiteSpace(value) ? "OPEN" : value); }

        public string NewApplicantNo { get => _newApplicantNo; set => SetField(ref _newApplicantNo, value ?? string.Empty); }
        public string NewApplicantFirstName { get => _newApplicantFirstName; set => SetField(ref _newApplicantFirstName, value ?? string.Empty); }
        public string NewApplicantLastName { get => _newApplicantLastName; set => SetField(ref _newApplicantLastName, value ?? string.Empty); }
        public string NewApplicantMiddleName { get => _newApplicantMiddleName; set => SetField(ref _newApplicantMiddleName, value ?? string.Empty); }
        public string NewApplicantEmail { get => _newApplicantEmail; set => SetField(ref _newApplicantEmail, value ?? string.Empty); }
        public string NewApplicantMobile { get => _newApplicantMobile; set => SetField(ref _newApplicantMobile, value ?? string.Empty); }
        public string NewApplicantAddress { get => _newApplicantAddress; set => SetField(ref _newApplicantAddress, value ?? string.Empty); }
        public DateTime? NewApplicantBirthDate { get => _newApplicantBirthDate; set => SetField(ref _newApplicantBirthDate, value?.Date); }

        public long? SelectedApplicationApplicantId { get => _selectedApplicationApplicantId; set => SetField(ref _selectedApplicationApplicantId, value); }
        public long? SelectedApplicationPostingId { get => _selectedApplicationPostingId; set => SetField(ref _selectedApplicationPostingId, value); }
        public string NewApplicationStatus { get => _newApplicationStatus; set => SetField(ref _newApplicationStatus, string.IsNullOrWhiteSpace(value) ? "SUBMITTED" : value); }
        public string NewApplicationNotes { get => _newApplicationNotes; set => SetField(ref _newApplicationNotes, value ?? string.Empty); }

        public long? SelectedInterviewApplicationId { get => _selectedInterviewApplicationId; set => SetField(ref _selectedInterviewApplicationId, value); }
        public DateTime NewInterviewDate { get => _newInterviewDate; set => SetField(ref _newInterviewDate, value.Date); }
        public string NewInterviewTimeText { get => _newInterviewTimeText; set => SetField(ref _newInterviewTimeText, value ?? string.Empty); }
        public string NewInterviewType { get => _newInterviewType; set => SetField(ref _newInterviewType, string.IsNullOrWhiteSpace(value) ? "ONSITE" : value); }
        public int? SelectedInterviewerEmployeeId { get => _selectedInterviewerEmployeeId; set => SetField(ref _selectedInterviewerEmployeeId, value); }
        public string NewInterviewStatus { get => _newInterviewStatus; set => SetField(ref _newInterviewStatus, string.IsNullOrWhiteSpace(value) ? "SCHEDULED" : value); }
        public string NewInterviewLocation { get => _newInterviewLocation; set => SetField(ref _newInterviewLocation, value ?? string.Empty); }
        public string NewInterviewRemarks { get => _newInterviewRemarks; set => SetField(ref _newInterviewRemarks, value ?? string.Empty); }

        public long? SelectedOfferApplicationId { get => _selectedOfferApplicationId; set => SetField(ref _selectedOfferApplicationId, value); }
        public string NewOfferStatus { get => _newOfferStatus; set => SetField(ref _newOfferStatus, string.IsNullOrWhiteSpace(value) ? "PENDING" : value); }
        public string NewOfferSalaryText { get => _newOfferSalaryText; set => SetField(ref _newOfferSalaryText, value ?? string.Empty); }
        public DateTime? NewOfferStartDate { get => _newOfferStartDate; set => SetField(ref _newOfferStartDate, value?.Date); }
        public string NewOfferRemarks { get => _newOfferRemarks; set => SetField(ref _newOfferRemarks, value ?? string.Empty); }

        public async Task RefreshAsync()
        {
            IsBusy = true;
            try
            {
                await LoadLookupsAsync();
                await LoadStatsAsync();
                await LoadRowsAsync();

                SetActionMessage("Recruitment module refreshed.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to refresh recruitment: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadLookupsAsync()
        {
            var departments = await _dataService.GetDepartmentsAsync();
            var positions = await _dataService.GetPositionsAsync();
            var employees = await _dataService.GetEmployeesAsync();
            var postingOptions = await _dataService.GetPostingOptionsAsync();
            var applicantOptions = await _dataService.GetApplicantOptionsAsync();
            var applicationOptions = await _dataService.GetApplicationOptionsAsync();

            ReplaceCollection(DepartmentOptions, departments.Select(d => new DepartmentOptionVm(d.DepartmentId, d.Name)));

            _allPositionOptions.Clear();
            _allPositionOptions.AddRange(positions.Select(p => new PositionOptionVm(p.PositionId, p.DepartmentId, p.Name)));
            RefreshNewPostingPositionOptions();

            ReplaceCollection(EmployeeOptions, employees.Select(e => new EmployeeOptionVm(e.EmployeeId, $"{e.EmployeeNo} - {e.EmployeeName}")));
            ReplaceCollection(PostingOptions, postingOptions.Select(p => new PostingOptionVm(p.JobPostingId, $"{p.PostingCode} - {p.Title}", p.Status)));
            ReplaceCollection(ApplicantOptions, applicantOptions.Select(a => new ApplicantOptionVm(a.ApplicantId, $"{a.ApplicantNo} - {a.ApplicantName}")));
            ReplaceCollection(ApplicationOptions,
                applicationOptions.Select(a => new ApplicationOptionVm(
                    a.JobApplicationId,
                    $"{a.PostingCode} | {a.ApplicantName}",
                    a.Status)));
        }

        private async Task LoadStatsAsync()
        {
            var stats = await _dataService.GetStatsAsync();
            TotalPostings = stats.TotalPostings;
            OpenPostings = stats.OpenPostings;
            TotalApplicants = stats.TotalApplicants;
            TotalApplications = stats.TotalApplications;
            ScheduledInterviews = stats.ScheduledInterviews;
            PendingOffers = stats.PendingOffers;
        }

        private async Task LoadRowsAsync()
        {
            var postings = await _dataService.GetJobPostingsAsync();
            var applicants = await _dataService.GetApplicantsAsync();
            var applications = await _dataService.GetApplicationsAsync();
            var interviews = await _dataService.GetInterviewsAsync();
            var offers = await _dataService.GetOffersAsync();
            _allPostings.Clear();
            _allPostings.AddRange(postings.Select(p => new JobPostingRowVm
            {
                JobPostingId = p.JobPostingId,
                PostingCode = p.PostingCode,
                Title = p.Title,
                DepartmentId = p.DepartmentId,
                DepartmentName = p.DepartmentName,
                PositionId = p.PositionId,
                PositionName = p.PositionName,
                EmploymentType = p.EmploymentType,
                Vacancies = p.Vacancies,
                Status = p.Status,
                OpenDate = p.OpenDate,
                CloseDate = p.CloseDate,
                ApplicationCount = p.ApplicationCount
            }));

            _allApplicants.Clear();
            _allApplicants.AddRange(applicants.Select(a => new ApplicantRowVm
            {
                ApplicantId = a.ApplicantId,
                ApplicantNo = a.ApplicantNo,
                FirstName = a.FirstName,
                LastName = a.LastName,
                MiddleName = a.MiddleName,
                Email = a.Email,
                MobileNo = a.MobileNo,
                Address = a.Address,
                BirthDate = a.BirthDate,
                CreatedAt = a.CreatedAt,
                ApplicationCount = a.ApplicationCount
            }));

            _allApplications.Clear();
            _allApplications.AddRange(applications.Select(a => new ApplicationRowVm
            {
                JobApplicationId = a.JobApplicationId,
                ApplicantId = a.ApplicantId,
                ApplicantNo = a.ApplicantNo,
                ApplicantName = a.ApplicantName,
                JobPostingId = a.JobPostingId,
                PostingCode = a.PostingCode,
                PostingTitle = a.PostingTitle,
                DepartmentName = a.DepartmentName,
                AppliedAt = a.AppliedAt,
                Status = a.Status,
                Notes = a.Notes,
                InterviewCount = a.InterviewCount,
                OfferCount = a.OfferCount
            }));

            _allInterviews.Clear();
            _allInterviews.AddRange(interviews.Select(i => new InterviewRowVm
            {
                InterviewScheduleId = i.InterviewScheduleId,
                JobApplicationId = i.JobApplicationId,
                ApplicantName = i.ApplicantName,
                PostingCode = i.PostingCode,
                PostingTitle = i.PostingTitle,
                InterviewDate = i.InterviewDateTime.Date,
                InterviewTimeText = i.InterviewDateTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                InterviewType = i.InterviewType,
                Location = i.Location,
                InterviewerEmployeeId = i.InterviewerEmployeeId,
                InterviewerName = i.InterviewerName,
                Status = i.Status,
                Remarks = i.Remarks
            }));

            _allOffers.Clear();
            _allOffers.AddRange(offers.Select(o => new OfferRowVm
            {
                JobOfferId = o.JobOfferId,
                JobApplicationId = o.JobApplicationId,
                ApplicantName = o.ApplicantName,
                PostingCode = o.PostingCode,
                PostingTitle = o.PostingTitle,
                OfferedAt = o.OfferedAt,
                OfferStatus = o.OfferStatus,
                SalaryOfferText = o.SalaryOffer?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
                StartDate = o.StartDate,
                Remarks = o.Remarks
            }));

            ApplyPostingFilters();
            ApplyApplicantFilters();
            ApplyApplicationFilters();
            ApplyInterviewFilters();
            ApplyOfferFilters();
        }

        private void ApplyPostingFilters()
        {
            var query = (PostingSearchText ?? string.Empty).Trim();
            var status = NormalizeFilter(SelectedPostingStatusFilter);

            IEnumerable<JobPostingRowVm> filtered = _allPostings;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(p => ContainsIgnoreCase(p.PostingCode, query)
                    || ContainsIgnoreCase(p.Title, query)
                    || ContainsIgnoreCase(p.DepartmentName, query)
                    || ContainsIgnoreCase(p.PositionName, query));
            }

            ReplaceCollection(JobPostings, filtered.OrderByDescending(p => p.OpenDate).ThenByDescending(p => p.JobPostingId));
        }

        private void ApplyApplicantFilters()
        {
            var query = (ApplicantSearchText ?? string.Empty).Trim();
            IEnumerable<ApplicantRowVm> filtered = _allApplicants;

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(a => ContainsIgnoreCase(a.ApplicantNo, query)
                    || ContainsIgnoreCase(a.FullName, query)
                    || ContainsIgnoreCase(a.Email, query)
                    || ContainsIgnoreCase(a.MobileNo, query));
            }

            ReplaceCollection(Applicants, filtered.OrderBy(a => a.LastName).ThenBy(a => a.FirstName));
        }

        private void ApplyApplicationFilters()
        {
            var query = (ApplicationSearchText ?? string.Empty).Trim();
            var status = NormalizeFilter(SelectedApplicationStatusFilter);
            IEnumerable<ApplicationRowVm> filtered = _allApplications;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(a => string.Equals(a.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(a => ContainsIgnoreCase(a.ApplicantName, query)
                    || ContainsIgnoreCase(a.PostingCode, query)
                    || ContainsIgnoreCase(a.PostingTitle, query)
                    || ContainsIgnoreCase(a.DepartmentName, query));
            }

            ReplaceCollection(Applications, filtered.OrderByDescending(a => a.AppliedAt).ThenByDescending(a => a.JobApplicationId));
        }

        private void ApplyInterviewFilters()
        {
            var query = (InterviewSearchText ?? string.Empty).Trim();
            var status = NormalizeFilter(SelectedInterviewStatusFilter);
            IEnumerable<InterviewRowVm> filtered = _allInterviews;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(i => ContainsIgnoreCase(i.ApplicantName, query)
                    || ContainsIgnoreCase(i.PostingCode, query)
                    || ContainsIgnoreCase(i.InterviewerName, query)
                    || ContainsIgnoreCase(i.Location, query));
            }

            ReplaceCollection(Interviews, filtered.OrderByDescending(i => i.InterviewDate).ThenByDescending(i => i.InterviewScheduleId));
        }

        private void ApplyOfferFilters()
        {
            var query = (OfferSearchText ?? string.Empty).Trim();
            var status = NormalizeFilter(SelectedOfferStatusFilter);
            IEnumerable<OfferRowVm> filtered = _allOffers;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(o => string.Equals(o.OfferStatus, status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(o => ContainsIgnoreCase(o.ApplicantName, query)
                    || ContainsIgnoreCase(o.PostingCode, query)
                    || ContainsIgnoreCase(o.Remarks, query));
            }

            ReplaceCollection(Offers, filtered.OrderByDescending(o => o.OfferedAt).ThenByDescending(o => o.JobOfferId));
        }

        private async Task AddPostingAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPostingCode) || string.IsNullOrWhiteSpace(NewPostingTitle))
            {
                SetActionMessage("Posting code and title are required.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.AddJobPostingAsync(
                    NewPostingCode,
                    NewPostingTitle,
                    SelectedNewDepartmentId,
                    SelectedNewPositionId,
                    NewPostingEmploymentType,
                    NewPostingVacancies,
                    NewPostingOpenDate,
                    NewPostingCloseDate,
                    NewPostingStatus);

                ClearPostingForm();
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentPostingAdded");
                SetActionMessage("Job posting added.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage(ex.Message, ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SavePostingAsync(object? parameter)
        {
            if (parameter is not JobPostingRowVm row)
            {
                return;
            }
            IsBusy = true;
            try
            {
                await _dataService.UpdateJobPostingAsync(
                    row.JobPostingId,
                    row.DepartmentId,
                    row.PositionId,
                    row.EmploymentType,
                    row.Vacancies,
                    row.Status,
                    row.OpenDate,
                    row.CloseDate);

                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentPostingUpdated");
                SetActionMessage($"Posting {row.PostingCode} updated.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to save posting: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeletePostingAsync(object? parameter)
        {
            if (parameter is not JobPostingRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.DeleteJobPostingAsync(row.JobPostingId);
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentPostingDeleted");
                SetActionMessage($"Posting {row.PostingCode} deleted.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to delete posting: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddApplicantAsync()
        {
            if (string.IsNullOrWhiteSpace(NewApplicantNo) || string.IsNullOrWhiteSpace(NewApplicantFirstName) || string.IsNullOrWhiteSpace(NewApplicantLastName))
            {
                SetActionMessage("Applicant no, first name, and last name are required.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.AddApplicantAsync(
                    NewApplicantNo,
                    NewApplicantFirstName,
                    NewApplicantLastName,
                    NewApplicantMiddleName,
                    NewApplicantEmail,
                    NewApplicantMobile,
                    NewApplicantAddress,
                    NewApplicantBirthDate);

                ClearApplicantForm();
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentApplicantAdded");
                SetActionMessage("Applicant added.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to add applicant: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveApplicantAsync(object? parameter)
        {
            if (parameter is not ApplicantRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.UpdateApplicantAsync(
                    row.ApplicantId,
                    row.FirstName,
                    row.LastName,
                    row.MiddleName,
                    row.Email,
                    row.MobileNo,
                    row.Address,
                    row.BirthDate);

                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentApplicantUpdated");
                SetActionMessage($"Applicant {row.ApplicantNo} updated.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to save applicant: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteApplicantAsync(object? parameter)
        {
            if (parameter is not ApplicantRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.DeleteApplicantAsync(row.ApplicantId);
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentApplicantDeleted");
                SetActionMessage($"Applicant {row.ApplicantNo} deleted.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to delete applicant: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddApplicationAsync()
        {
            if (!SelectedApplicationApplicantId.HasValue || SelectedApplicationApplicantId <= 0 || !SelectedApplicationPostingId.HasValue || SelectedApplicationPostingId <= 0)
            {
                SetActionMessage("Select an applicant and posting first.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.AddApplicationAsync(
                    SelectedApplicationApplicantId.Value,
                    SelectedApplicationPostingId.Value,
                    NewApplicationStatus,
                    NewApplicationNotes);

                ClearApplicationForm();
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentApplicationAdded");
                SetActionMessage("Application added to pipeline.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to add application: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveApplicationAsync(object? parameter)
        {
            if (parameter is not ApplicationRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.UpdateApplicationAsync(row.JobApplicationId, row.Status, row.Notes);
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentApplicationUpdated");
                SetActionMessage($"Application #{row.JobApplicationId} updated.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to save application: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteApplicationAsync(object? parameter)
        {
            if (parameter is not ApplicationRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.DeleteApplicationAsync(row.JobApplicationId);
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentApplicationDeleted");
                SetActionMessage($"Application #{row.JobApplicationId} deleted.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to delete application: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task AddInterviewAsync()
        {
            if (!SelectedInterviewApplicationId.HasValue || SelectedInterviewApplicationId <= 0)
            {
                SetActionMessage("Select an application for the interview.", ErrorBrush);
                return;
            }

            if (!TryBuildDateTime(NewInterviewDate, NewInterviewTimeText, out var dateTime))
            {
                SetActionMessage("Invalid interview time. Use HH:mm format.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.AddInterviewAsync(
                    SelectedInterviewApplicationId.Value,
                    dateTime,
                    NewInterviewType,
                    NewInterviewLocation,
                    SelectedInterviewerEmployeeId,
                    NewInterviewStatus,
                    NewInterviewRemarks);

                ClearInterviewForm();
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentInterviewAdded");
                SetActionMessage("Interview schedule added.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to add interview: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveInterviewAsync(object? parameter)
        {
            if (parameter is not InterviewRowVm row)
            {
                return;
            }

            if (!TryBuildDateTime(row.InterviewDate, row.InterviewTimeText, out var dateTime))
            {
                SetActionMessage("Invalid row interview time. Use HH:mm.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.UpdateInterviewAsync(
                    row.InterviewScheduleId,
                    dateTime,
                    row.InterviewType,
                    row.Location,
                    row.InterviewerEmployeeId,
                    row.Status,
                    row.Remarks);

                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentInterviewUpdated");
                SetActionMessage($"Interview #{row.InterviewScheduleId} updated.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to save interview: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteInterviewAsync(object? parameter)
        {
            if (parameter is not InterviewRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.DeleteInterviewAsync(row.InterviewScheduleId);
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentInterviewDeleted");
                SetActionMessage($"Interview #{row.InterviewScheduleId} deleted.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to delete interview: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddOfferAsync()
        {
            if (!SelectedOfferApplicationId.HasValue || SelectedOfferApplicationId <= 0)
            {
                SetActionMessage("Select an application before creating an offer.", ErrorBrush);
                return;
            }

            if (!TryParseNullableDecimal(NewOfferSalaryText, out var salaryOffer))
            {
                SetActionMessage("Salary offer must be a valid number.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.AddOfferAsync(
                    SelectedOfferApplicationId.Value,
                    salaryOffer,
                    NewOfferStartDate,
                    NewOfferStatus,
                    NewOfferRemarks);

                ClearOfferForm();
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentOfferAdded");
                SetActionMessage("Job offer added.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to add offer: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveOfferAsync(object? parameter)
        {
            if (parameter is not OfferRowVm row)
            {
                return;
            }

            if (!TryParseNullableDecimal(row.SalaryOfferText, out var salaryOffer))
            {
                SetActionMessage("Salary offer in row is not valid.", ErrorBrush);
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.UpdateOfferAsync(
                    row.JobOfferId,
                    row.OfferStatus,
                    salaryOffer,
                    row.StartDate,
                    row.Remarks);

                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentOfferUpdated");
                SetActionMessage($"Offer #{row.JobOfferId} updated.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to save offer: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteOfferAsync(object? parameter)
        {
            if (parameter is not OfferRowVm row)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _dataService.DeleteOfferAsync(row.JobOfferId);
                await RefreshAsync();
                SystemRefreshBus.Raise("RecruitmentOfferDeleted");
                SetActionMessage($"Offer #{row.JobOfferId} deleted.", SuccessBrush);
            }
            catch (Exception ex)
            {
                SetActionMessage($"Unable to delete offer: {ex.Message}", ErrorBrush);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearPostingForm()
        {
            NewPostingCode = string.Empty;
            NewPostingTitle = string.Empty;
            SelectedNewDepartmentId = null;
            SelectedNewPositionId = null;
            NewPostingEmploymentType = "CASUAL";
            NewPostingVacancies = 1;
            NewPostingOpenDate = DateTime.Today;
            NewPostingCloseDate = null;
            NewPostingStatus = "OPEN";
        }

        private void ClearApplicantForm()
        {
            NewApplicantNo = string.Empty;
            NewApplicantFirstName = string.Empty;
            NewApplicantLastName = string.Empty;
            NewApplicantMiddleName = string.Empty;
            NewApplicantEmail = string.Empty;
            NewApplicantMobile = string.Empty;
            NewApplicantAddress = string.Empty;
            NewApplicantBirthDate = null;
        }

        private void ClearApplicationForm()
        {
            SelectedApplicationApplicantId = null;
            SelectedApplicationPostingId = null;
            NewApplicationStatus = "SUBMITTED";
            NewApplicationNotes = string.Empty;
        }

        private void ClearInterviewForm()
        {
            SelectedInterviewApplicationId = null;
            NewInterviewDate = DateTime.Today;
            NewInterviewTimeText = "09:00";
            NewInterviewType = "ONSITE";
            SelectedInterviewerEmployeeId = null;
            NewInterviewStatus = "SCHEDULED";
            NewInterviewLocation = string.Empty;
            NewInterviewRemarks = string.Empty;
        }

        private void ClearOfferForm()
        {
            SelectedOfferApplicationId = null;
            NewOfferStatus = "PENDING";
            NewOfferSalaryText = string.Empty;
            NewOfferStartDate = null;
            NewOfferRemarks = string.Empty;
        }

        private void RefreshNewPostingPositionOptions()
        {
            var filtered = _allPositionOptions
                .Where(p => !SelectedNewDepartmentId.HasValue || p.DepartmentId == SelectedNewDepartmentId)
                .OrderBy(p => p.Name)
                .ToList();

            ReplaceCollection(NewPostingPositionOptions, filtered);

            if (SelectedNewPositionId.HasValue && filtered.All(p => p.PositionId != SelectedNewPositionId.Value))
            {
                SelectedNewPositionId = null;
            }
        }

        private void SetActionMessage(string message, Brush brush)
        {
            ActionMessage = message;
            ActionMessageBrush = brush;
        }

        private static bool TryBuildDateTime(DateTime date, string? timeText, out DateTime result)
        {
            result = date.Date;
            if (string.IsNullOrWhiteSpace(timeText))
            {
                return true;
            }

            if (TimeSpan.TryParseExact(timeText.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var time)
                || TimeSpan.TryParse(timeText.Trim(), CultureInfo.CurrentCulture, out time))
            {
                result = date.Date.Add(time);
                return true;
            }

            return false;
        }

        private static bool TryParseNullableDecimal(string? text, out decimal? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var current)
                || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out current))
            {
                value = current;
                return true;
            }

            return false;
        }
        private static string? NormalizeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return value.Trim();
        }

        private static bool ContainsIgnoreCase(string? source, string term)
        {
            return !string.IsNullOrWhiteSpace(source) && source.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items)
            {
                target.Add(item);
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public abstract class RecruitmentNotifyVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void Raise([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class DepartmentOptionVm
    {
        public DepartmentOptionVm(int departmentId, string name)
        {
            DepartmentId = departmentId;
            Name = name;
        }

        public int DepartmentId { get; }
        public string Name { get; }
    }

    public sealed class PositionOptionVm
    {
        public PositionOptionVm(int positionId, int? departmentId, string name)
        {
            PositionId = positionId;
            DepartmentId = departmentId;
            Name = name;
        }

        public int PositionId { get; }
        public int? DepartmentId { get; }
        public string Name { get; }
    }

    public sealed class EmployeeOptionVm
    {
        public EmployeeOptionVm(int employeeId, string label)
        {
            EmployeeId = employeeId;
            Label = label;
        }

        public int EmployeeId { get; }
        public string Label { get; }
    }

    public sealed class PostingOptionVm
    {
        public PostingOptionVm(long jobPostingId, string label, string status)
        {
            JobPostingId = jobPostingId;
            Label = label;
            Status = status;
        }

        public long JobPostingId { get; }
        public string Label { get; }
        public string Status { get; }
    }

    public sealed class ApplicantOptionVm
    {
        public ApplicantOptionVm(long applicantId, string label)
        {
            ApplicantId = applicantId;
            Label = label;
        }

        public long ApplicantId { get; }
        public string Label { get; }
    }

    public sealed class ApplicationOptionVm
    {
        public ApplicationOptionVm(long jobApplicationId, string label, string status)
        {
            JobApplicationId = jobApplicationId;
            Label = label;
            Status = status;
        }

        public long JobApplicationId { get; }
        public string Label { get; }
        public string Status { get; }
    }

    public sealed class JobPostingRowVm : RecruitmentNotifyVm
    {
        private string _title = string.Empty;
        private int? _departmentId;
        private string _departmentName = "-";
        private int? _positionId;
        private string _positionName = "-";
        private string _employmentType = "CASUAL";
        private int _vacancies = 1;
        private string _status = "OPEN";
        private DateTime _openDate = DateTime.Today;
        private DateTime? _closeDate;
        private int _applicationCount;

        public long JobPostingId { get; set; }
        public string PostingCode { get; set; } = string.Empty;

        public string Title { get => _title; set => SetField(ref _title, value ?? string.Empty); }
        public int? DepartmentId { get => _departmentId; set => SetField(ref _departmentId, value); }
        public string DepartmentName { get => _departmentName; set => SetField(ref _departmentName, value ?? "-"); }
        public int? PositionId { get => _positionId; set => SetField(ref _positionId, value); }
        public string PositionName { get => _positionName; set => SetField(ref _positionName, value ?? "-"); }
        public string EmploymentType { get => _employmentType; set => SetField(ref _employmentType, string.IsNullOrWhiteSpace(value) ? "CASUAL" : value); }
        public int Vacancies { get => _vacancies; set => SetField(ref _vacancies, value < 1 ? 1 : value); }
        public string Status { get => _status; set => SetField(ref _status, string.IsNullOrWhiteSpace(value) ? "OPEN" : value); }

        public DateTime OpenDate
        {
            get => _openDate;
            set
            {
                if (SetField(ref _openDate, value.Date))
                {
                    Raise(nameof(OpenDateText));
                }
            }
        }

        public DateTime? CloseDate
        {
            get => _closeDate;
            set
            {
                if (SetField(ref _closeDate, value?.Date))
                {
                    Raise(nameof(CloseDateText));
                }
            }
        }

        public int ApplicationCount { get => _applicationCount; set => SetField(ref _applicationCount, value); }
        public string OpenDateText => OpenDate.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
        public string CloseDateText => CloseDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "-";
    }

    public sealed class ApplicantRowVm : RecruitmentNotifyVm
    {
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _middleName = string.Empty;
        private string _email = string.Empty;
        private string _mobileNo = string.Empty;
        private string _address = string.Empty;
        private DateTime? _birthDate;
        private DateTime _createdAt = DateTime.Today;
        private int _applicationCount;

        public long ApplicantId { get; set; }
        public string ApplicantNo { get; set; } = string.Empty;

        public string FirstName
        {
            get => _firstName;
            set
            {
                if (SetField(ref _firstName, value ?? string.Empty))
                {
                    Raise(nameof(FullName));
                }
            }
        }

        public string LastName
        {
            get => _lastName;
            set
            {
                if (SetField(ref _lastName, value ?? string.Empty))
                {
                    Raise(nameof(FullName));
                }
            }
        }

        public string MiddleName
        {
            get => _middleName;
            set
            {
                if (SetField(ref _middleName, value ?? string.Empty))
                {
                    Raise(nameof(FullName));
                }
            }
        }

        public string Email { get => _email; set => SetField(ref _email, value ?? string.Empty); }
        public string MobileNo { get => _mobileNo; set => SetField(ref _mobileNo, value ?? string.Empty); }
        public string Address { get => _address; set => SetField(ref _address, value ?? string.Empty); }
        public DateTime? BirthDate { get => _birthDate; set => SetField(ref _birthDate, value?.Date); }
        public DateTime CreatedAt { get => _createdAt; set => SetField(ref _createdAt, value); }
        public int ApplicationCount { get => _applicationCount; set => SetField(ref _applicationCount, value); }

        public string FullName
        {
            get
            {
                var middle = string.IsNullOrWhiteSpace(MiddleName) ? string.Empty : $" {MiddleName}";
                return $"{LastName}, {FirstName}{middle}".Trim().Trim(',');
            }
        }
    }

    public sealed class ApplicationRowVm : RecruitmentNotifyVm
    {
        private string _status = "SUBMITTED";
        private string _notes = string.Empty;

        public long JobApplicationId { get; set; }
        public long ApplicantId { get; set; }
        public string ApplicantNo { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public long JobPostingId { get; set; }
        public string PostingCode { get; set; } = string.Empty;
        public string PostingTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public int InterviewCount { get; set; }
        public int OfferCount { get; set; }

        public string Status { get => _status; set => SetField(ref _status, string.IsNullOrWhiteSpace(value) ? "SUBMITTED" : value); }
        public string Notes { get => _notes; set => SetField(ref _notes, value ?? string.Empty); }
    }

    public sealed class InterviewRowVm : RecruitmentNotifyVm
    {
        private DateTime _interviewDate = DateTime.Today;
        private string _interviewTimeText = "09:00";
        private string _interviewType = "ONSITE";
        private string _location = string.Empty;
        private int? _interviewerEmployeeId;
        private string _interviewerName = "-";
        private string _status = "SCHEDULED";
        private string _remarks = string.Empty;

        public long InterviewScheduleId { get; set; }
        public long JobApplicationId { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string PostingCode { get; set; } = string.Empty;
        public string PostingTitle { get; set; } = string.Empty;

        public DateTime InterviewDate { get => _interviewDate; set => SetField(ref _interviewDate, value.Date); }
        public string InterviewTimeText { get => _interviewTimeText; set => SetField(ref _interviewTimeText, value ?? string.Empty); }
        public string InterviewType { get => _interviewType; set => SetField(ref _interviewType, string.IsNullOrWhiteSpace(value) ? "ONSITE" : value); }
        public string Location { get => _location; set => SetField(ref _location, value ?? string.Empty); }
        public int? InterviewerEmployeeId { get => _interviewerEmployeeId; set => SetField(ref _interviewerEmployeeId, value); }
        public string InterviewerName { get => _interviewerName; set => SetField(ref _interviewerName, value ?? "-"); }
        public string Status { get => _status; set => SetField(ref _status, string.IsNullOrWhiteSpace(value) ? "SCHEDULED" : value); }
        public string Remarks { get => _remarks; set => SetField(ref _remarks, value ?? string.Empty); }
    }

    public sealed class OfferRowVm : RecruitmentNotifyVm
    {
        private string _offerStatus = "PENDING";
        private string _salaryOfferText = string.Empty;
        private DateTime? _startDate;
        private string _remarks = string.Empty;

        public long JobOfferId { get; set; }
        public long JobApplicationId { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string PostingCode { get; set; } = string.Empty;
        public string PostingTitle { get; set; } = string.Empty;
        public DateTime OfferedAt { get; set; }

        public string OfferStatus { get => _offerStatus; set => SetField(ref _offerStatus, string.IsNullOrWhiteSpace(value) ? "PENDING" : value); }
        public string SalaryOfferText { get => _salaryOfferText; set => SetField(ref _salaryOfferText, value ?? string.Empty); }
        public DateTime? StartDate { get => _startDate; set => SetField(ref _startDate, value?.Date); }
        public string Remarks { get => _remarks; set => SetField(ref _remarks, value ?? string.Empty); }
    }
}

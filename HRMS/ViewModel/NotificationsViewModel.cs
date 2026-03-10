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
using System.Windows;

namespace HRMS.ViewModel
{
    public class NotificationsViewModel : INotifyPropertyChanged
    {
        private readonly EmployeeSelfService _dataService = new(DbConfig.ConnectionString);
        private readonly ObservableCollection<NotificationRowVm> _allNotifications = new();
        private readonly HashSet<string> _locallyReadKeys = new(StringComparer.OrdinalIgnoreCase);
        private int _currentUserId;
        private int? _currentEmployeeId;
        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _selectedModule = "All";
        private string _statusMessage = "Ready.";
        private Brush _statusBrush = Brushes.SeaGreen;

        public NotificationsViewModel()
        {
            ModuleOptions.Add("All");
            ModuleOptions.Add("Attendance");
            ModuleOptions.Add("Adjustments");
            ModuleOptions.Add("Leave");
            ModuleOptions.Add("Payroll");
            ModuleOptions.Add("Development");

            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
            OpenSourceCommand = new AsyncRelayCommand(OpenSourceAsync);
        }

        public ObservableCollection<string> ModuleOptions { get; } = new();
        public ObservableCollection<NotificationRowVm> Notifications { get; } = new();

        public int UnreadCount => _allNotifications.Count(x => !x.IsRead);

        public ICommand RefreshCommand { get; }
        public ICommand OpenSourceCommand { get; }

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

        public string SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (SetField(ref _selectedModule, value))
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
            var nextUserId = user?.UserId ?? 0;
            if (nextUserId != _currentUserId)
            {
                _locallyReadKeys.Clear();
            }

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
                    _allNotifications.Clear();
                    Notifications.Clear();
                    SetMessage("Employee profile is not linked to this account.", Brushes.IndianRed);
                    return;
                }

                var data = await _dataService.GetEmployeeNotificationsAsync(_currentEmployeeId.Value, 500);
                RebuildRows(data);
                ApplyFilter();
                SetMessage($"Loaded {Notifications.Count} notification(s).", Brushes.SeaGreen);
            }
            catch (Exception ex)
            {
                SetMessage($"Unable to load notifications: {ex.Message}", Brushes.IndianRed);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OpenSourceAsync(object? parameter)
        {
            if (parameter is not NotificationRowVm row)
            {
                return;
            }

            if (_currentEmployeeId.HasValue && _currentEmployeeId.Value > 0 && !row.IsRead)
            {
                _locallyReadKeys.Add(BuildReadKey(row.ModuleKey, row.SourceId, row.EventAt));
                await _dataService.MarkEmployeeNotificationAsReadAsync(_currentEmployeeId.Value, row.ModuleKey, row.SourceId, row.EventAt);
                row.MarkAsRead();
                OnPropertyChanged(nameof(UnreadCount));
            }

            await Task.Yield();
            OpenModuleRequested?.Invoke(this, row.ModuleKey);
        }

        private void RebuildRows(IReadOnlyList<EmployeeNotificationDto> data)
        {
            _allNotifications.Clear();
            foreach (var item in data)
            {
                var isRead = item.IsRead || _locallyReadKeys.Contains(BuildReadKey(item.ModuleKey, item.SourceId, item.EventAt));
                _allNotifications.Add(new NotificationRowVm(item with { IsRead = isRead }));
            }

            OnPropertyChanged(nameof(UnreadCount));
        }

        private static string BuildReadKey(string moduleKey, long sourceId, DateTime eventAt)
        {
            var normalizedModule = string.IsNullOrWhiteSpace(moduleKey)
                ? "DASHBOARD"
                : moduleKey.Trim().ToUpperInvariant();

            var normalizedEventAt = eventAt == DateTime.MinValue
                ? "0001-01-01 00:00:00"
                : eventAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            return $"{normalizedModule}|{sourceId}|{normalizedEventAt}";
        }

        private void ApplyFilter()
        {
            var query = (SearchText ?? string.Empty).Trim();
            var selectedModule = (SelectedModule ?? "All").Trim();

            var source = _allNotifications.AsEnumerable();

            if (!string.Equals(selectedModule, "All", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(x => string.Equals(x.ModuleLabel, selectedModule, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                source = source.Where(x =>
                    ContainsText(x.ModuleLabel, query) ||
                    ContainsText(x.Title, query) ||
                    ContainsText(x.Message, query) ||
                    ContainsText(x.Status, query));
            }

            Notifications.Clear();
            foreach (var item in source)
            {
                Notifications.Add(item);
            }

            OnPropertyChanged(nameof(UnreadCount));
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
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NotificationRowVm : INotifyPropertyChanged
    {
        private bool _isRead;

        public NotificationRowVm(EmployeeNotificationDto dto)
        {
            SourceId = dto.SourceId;
            ModuleKey = string.IsNullOrWhiteSpace(dto.ModuleKey) ? "DASHBOARD" : dto.ModuleKey.Trim().ToUpperInvariant();
            ModuleLabel = ModuleKey switch
            {
                "LEAVE" => "Leave",
                "ADJUSTMENTS" => "Adjustments",
                "PAYROLL" => "Payroll",
                "DEVELOPMENT" => "Development",
                _ => "Attendance"
            };
            Title = dto.Title;
            Message = dto.Message;
            Status = dto.Status;
            EventAt = dto.EventAt;
            EventAtText = dto.EventAt == DateTime.MinValue
                ? "-"
                : dto.EventAt.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);
            StatusBrush = ResolveStatusBrush(dto.Status);
            _isRead = dto.IsRead;
        }

        public long SourceId { get; }
        public string ModuleKey { get; }
        public string ModuleLabel { get; }
        public string Title { get; }
        public string Message { get; }
        public string Status { get; }
        public DateTime EventAt { get; }
        public string EventAtText { get; }
        public Brush StatusBrush { get; }
        public bool IsRead
        {
            get => _isRead;
            private set
            {
                if (_isRead == value)
                {
                    return;
                }

                _isRead = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRead)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemOpacity)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleFontWeight)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnreadDotVisibility)));
            }
        }

        public double ItemOpacity => IsRead ? 0.72 : 1.0;
        public FontWeight TitleFontWeight => IsRead ? FontWeights.Medium : FontWeights.SemiBold;
        public Visibility UnreadDotVisibility => IsRead ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void MarkAsRead() => IsRead = true;

        private static Brush ResolveStatusBrush(string status)
        {
            var normalized = status?.Trim().ToUpperInvariant() ?? string.Empty;
            return normalized switch
            {
                "APPROVED" => Brushes.SeaGreen,
                "RESOLVED" => Brushes.SeaGreen,
                "RELEASED" => Brushes.SeaGreen,
                "COMPLETED" => Brushes.SeaGreen,
                "REJECTED" => Brushes.IndianRed,
                "LOCKED" => Brushes.IndianRed,
                "PENDING" => Brushes.DarkGoldenrod,
                "SUBMITTED" => Brushes.DarkGoldenrod,
                "REQUESTED" => Brushes.DarkGoldenrod,
                "IN_REVIEW" => Brushes.DarkGoldenrod,
                _ => Brushes.SteelBlue
            };
        }
    }
}

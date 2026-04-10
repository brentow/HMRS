using HRMS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HRMS.View
{
    public partial class TransactionsWindow : UserControl
    {
        private readonly TransactionsDataService _dataService = new(DbConfig.ConnectionString);
        private readonly ObservableCollection<TransactionGridRow> _rows = new();

        private int _currentUserId;
        private bool _restrictToCurrentUser;
        private bool _isLoadedOnce;
        private bool _isRefreshing;

        public TransactionsWindow()
        {
            InitializeComponent();
            TransactionsDataGrid.ItemsSource = _rows;
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            _currentUserId = user?.UserId ?? 0;
            _restrictToCurrentUser = string.Equals(user?.RoleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
        }

        public async Task RefreshAsync()
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            StatusTextBlock.Text = "Loading transactions...";

            try
            {
                var actorFilterUserId = GetActorFilterUserId();
                var statusFilter = GetSelectedStatusFilter();
                var searchText = SearchTextBox.Text?.Trim() ?? string.Empty;

                var summaryTask = _dataService.GetSummaryAsync(actorFilterUserId);
                var logsTask = _dataService.GetTransactionsAsync(statusFilter, searchText, limit: 300, actedByUserId: actorFilterUserId);
                await Task.WhenAll(summaryTask, logsTask);

                ApplySummary(summaryTask.Result);
                ApplyRows(logsTask.Result);

                StatusTextBlock.Text = _rows.Count == 0
                    ? "No transactions matched the current filter."
                    : $"Showing {_rows.Count:N0} transaction record(s).";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Unable to load transactions: {ex.Message}";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoadedOnce)
            {
                return;
            }

            _isLoadedOnce = true;
            await RefreshAsync();
        }

        private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void ApplyFiltersButton_OnClick(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void SearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            await RefreshAsync();
        }

        private async void StatusFilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoadedOnce || !IsLoaded)
            {
                return;
            }

            await RefreshAsync();
        }

        private int? GetActorFilterUserId()
        {
            if (_restrictToCurrentUser && _currentUserId > 0)
            {
                return _currentUserId;
            }

            return null;
        }

        private string GetSelectedStatusFilter()
        {
            if (StatusFilterComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string statusTag &&
                !string.IsNullOrWhiteSpace(statusTag))
            {
                return statusTag.Trim();
            }

            return "ALL";
        }

        private void ApplySummary(TransactionsSummaryDto summary)
        {
            TotalCountText.Text = summary.Total.ToString("N0", CultureInfo.InvariantCulture);
            SuccessCountText.Text = summary.Success.ToString("N0", CultureInfo.InvariantCulture);
            DeniedCountText.Text = summary.Denied.ToString("N0", CultureInfo.InvariantCulture);
            FailedCountText.Text = summary.Failed.ToString("N0", CultureInfo.InvariantCulture);
            TodayCountText.Text = summary.Today.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void ApplyRows(IReadOnlyList<TransactionEntryDto> rows)
        {
            _rows.Clear();
            foreach (var row in rows)
            {
                _rows.Add(TransactionGridRow.FromDto(row));
            }
        }

        private sealed class TransactionGridRow
        {
            public string ReferenceNo { get; init; } = "-";
            public string DateTimeText { get; init; } = "-";
            public string Action { get; init; } = "-";
            public string Module { get; init; } = "-";
            public string Target { get; init; } = "-";
            public string Status { get; init; } = "-";
            public string PerformedBy { get; init; } = "-";
            public string Details { get; init; } = "-";

            public static TransactionGridRow FromDto(TransactionEntryDto dto)
            {
                var target = string.IsNullOrWhiteSpace(dto.TargetId) ? "-" : dto.TargetId;
                var details = string.IsNullOrWhiteSpace(dto.Details) ? "-" : dto.Details;

                return new TransactionGridRow
                {
                    ReferenceNo = $"TRX-{dto.AuditLogId:D7}",
                    DateTimeText = dto.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    Action = dto.ActionCode,
                    Module = dto.TargetType,
                    Target = target,
                    Status = dto.ResultStatus,
                    PerformedBy = string.IsNullOrWhiteSpace(dto.Username) ? "System" : dto.Username,
                    Details = details
                };
            }
        }
    }
}

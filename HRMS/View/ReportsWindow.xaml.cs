using HRMS.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HRMS.View
{
    public partial class ReportsWindow : UserControl
    {
        private readonly ReportsDataService _reportsDataService = new(DbConfig.ConnectionString);
        private readonly CompanyProfileDataService _companyProfileService = new(DbConfig.ConnectionString);
        private readonly ReportExportService _reportExportService = new();

        private bool _isLoadedOnce;
        private bool _isRefreshing;
        private bool _suppressSelectionEvents;

        private int _currentUserId;
        private int? _currentEmployeeId;
        private bool _isEmployeeMode;

        private IReadOnlyList<ReportCatalogItem> _catalog = [];
        private ReportDataset? _currentDataset;

        public ReportsWindow()
        {
            InitializeComponent();
        }

        public void SetCurrentUser(AuthenticatedUser? user)
        {
            _currentUserId = user?.UserId ?? 0;
            _currentEmployeeId = user?.EmployeeId;
            _isEmployeeMode = string.Equals(user?.RoleName?.Trim(), "Employee", StringComparison.OrdinalIgnoreCase);
        }

        public async Task RefreshAsync()
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            try
            {
                LoadCatalog();

                if (ReportComboBox.SelectedItem is ReportOption selectedOption)
                {
                    await LoadReportAsync(selectedOption);
                }
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

        private async void LoadReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ReportComboBox.SelectedItem is not ReportOption selectedOption)
            {
                SetStatus("Select a report first.");
                return;
            }

            await LoadReportAsync(selectedOption);
        }

        private void CategoryComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvents)
            {
                return;
            }

            PopulateReports();
        }

        private void ReportComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvents)
            {
                return;
            }

            ApplySelectedReportContext();
        }

        private async void ExportPdfButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_currentDataset == null || _currentDataset.Table.Columns.Count == 0)
            {
                SetStatus("No report data to export.");
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Report to PDF",
                    Filter = "PDF File (*.pdf)|*.pdf",
                    FileName = BuildExportFileName(_currentDataset.ReportName, "pdf")
                };

                if (dialog.ShowDialog() != true)
                {
                    SetStatus("PDF export cancelled.");
                    return;
                }

                var profile = await _companyProfileService.GetCompanyProfileAsync();
                await _reportExportService.ExportPdfAsync(_currentDataset, profile, dialog.FileName);
                SetStatus($"PDF exported: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                SetStatus($"PDF export failed: {ex.Message}");
            }
        }

        private async void ExportExcelButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_currentDataset == null || _currentDataset.Table.Columns.Count == 0)
            {
                SetStatus("No report data to export.");
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Report to Excel",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = BuildExportFileName(_currentDataset.ReportName, "xlsx")
                };

                if (dialog.ShowDialog() != true)
                {
                    SetStatus("Excel export cancelled.");
                    return;
                }

                var profile = await _companyProfileService.GetCompanyProfileAsync();
                await _reportExportService.ExportExcelAsync(_currentDataset, profile, dialog.FileName);
                SetStatus($"Excel exported: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                SetStatus($"Excel export failed: {ex.Message}");
            }
        }

        private void LoadCatalog()
        {
            _suppressSelectionEvents = true;
            try
            {
                _catalog = _reportsDataService.GetCatalog(_isEmployeeMode);
                var categories = _reportsDataService.GetCategories(_isEmployeeMode)
                    .Select(category => new CategoryOption(category.CategoryKey, category.CategoryName))
                    .ToList();

                CategoryComboBox.ItemsSource = categories;
                if (categories.Count == 0)
                {
                    ReportComboBox.ItemsSource = Array.Empty<ReportOption>();
                    SelectedReportTitleText.Text = "No reports available.";
                    SelectedReportDescriptionText.Text = "Your current role has no report scope.";
                    RowsCountTextBlock.Text = "Rows: 0";
                    ReportDataGrid.ItemsSource = null;
                    _currentDataset = null;
                    SetStatus("No reports available for this account.");
                    return;
                }

                var currentCategory = CategoryComboBox.SelectedItem as CategoryOption;
                var restoredCategory = currentCategory != null
                    ? categories.FirstOrDefault(x => string.Equals(x.CategoryKey, currentCategory.CategoryKey, StringComparison.OrdinalIgnoreCase))
                    : null;
                CategoryComboBox.SelectedItem = restoredCategory ?? categories[0];

                PopulateReports();
            }
            finally
            {
                _suppressSelectionEvents = false;
            }
        }

        private void PopulateReports()
        {
            var selectedCategory = CategoryComboBox.SelectedItem as CategoryOption;
            if (selectedCategory == null)
            {
                ReportComboBox.ItemsSource = Array.Empty<ReportOption>();
                return;
            }

            var reports = _reportsDataService
                .GetReportsByCategory(selectedCategory.CategoryKey, _isEmployeeMode)
                .Select(item => new ReportOption(item))
                .ToList();

            ReportComboBox.ItemsSource = reports;

            var current = ReportComboBox.SelectedItem as ReportOption;
            var restored = current != null
                ? reports.FirstOrDefault(x => string.Equals(x.Key, current.Key, StringComparison.OrdinalIgnoreCase))
                : null;
            ReportComboBox.SelectedItem = restored ?? reports.FirstOrDefault();

            ApplySelectedReportContext();
        }

        private void ApplySelectedReportContext()
        {
            var selectedReport = ReportComboBox.SelectedItem as ReportOption;
            if (selectedReport == null)
            {
                SelectedReportTitleText.Text = "No report selected.";
                SelectedReportDescriptionText.Text = "Choose a report from the selected category.";
                DateFromPicker.IsEnabled = false;
                DateToPicker.IsEnabled = false;
                return;
            }

            SelectedReportTitleText.Text = selectedReport.ReportName;
            SelectedReportDescriptionText.Text = selectedReport.Description;
            DateFromPicker.IsEnabled = selectedReport.SupportsDateRange;
            DateToPicker.IsEnabled = selectedReport.SupportsDateRange;

            if (!selectedReport.SupportsDateRange)
            {
                DateFromPicker.SelectedDate = null;
                DateToPicker.SelectedDate = null;
            }
        }

        private async Task LoadReportAsync(ReportOption selectedOption)
        {
            try
            {
                SetStatus($"Loading {selectedOption.ReportName}...");

                var dateFrom = selectedOption.SupportsDateRange ? DateFromPicker.SelectedDate : null;
                var dateTo = selectedOption.SupportsDateRange ? DateToPicker.SelectedDate : null;

                var dataset = await _reportsDataService.LoadReportAsync(
                    reportKey: selectedOption.Key,
                    dateFrom: dateFrom,
                    dateTo: dateTo,
                    isEmployeeMode: _isEmployeeMode,
                    employeeId: _currentEmployeeId,
                    userId: _currentUserId > 0 ? _currentUserId : null);

                _currentDataset = dataset;
                ReportDataGrid.ItemsSource = dataset.Table.DefaultView;
                RowsCountTextBlock.Text = $"Rows: {dataset.Table.Rows.Count:N0}";

                SetStatus($"Loaded {dataset.ReportName} ({dataset.Table.Rows.Count:N0} rows).");
            }
            catch (Exception ex)
            {
                _currentDataset = null;
                ReportDataGrid.ItemsSource = null;
                RowsCountTextBlock.Text = "Rows: 0";
                SetStatus($"Unable to load report: {ex.Message}");
            }
        }

        private void SetStatus(string message)
        {
            StatusTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "Ready." : message.Trim();
        }

        private static string BuildExportFileName(string reportName, string extension)
        {
            var safeName = new string((reportName ?? "Report")
                .Trim()
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
                .ToArray());
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Report";
            }

            safeName = safeName.Replace(' ', '_');
            return $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.{extension}";
        }

        private sealed record CategoryOption(string CategoryKey, string CategoryName);

        private sealed class ReportOption
        {
            public ReportOption(ReportCatalogItem item)
            {
                Key = item.Key;
                ReportName = item.ReportName;
                Description = item.Description;
                SupportsDateRange = item.SupportsDateRange;
            }

            public string Key { get; }
            public string ReportName { get; }
            public string Description { get; }
            public bool SupportsDateRange { get; }
        }
    }
}

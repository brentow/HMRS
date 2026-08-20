using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QColors = QuestPDF.Helpers.Colors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class EmployeeAttendanceLogsWindow : Window
    {
        private readonly AttendanceEmployeeLogSummaryVm _employee;
        private readonly List<AttendanceEmployeeDailyLogVm> _rows;

        public EmployeeAttendanceLogsWindow(
            AttendanceEmployeeLogSummaryVm employee,
            IReadOnlyList<AttendanceEmployeeDailyLogVm> rows)
        {
            InitializeComponent();

            _employee = employee;
            _rows = rows?.ToList() ?? new List<AttendanceEmployeeDailyLogVm>();

            HeaderTextBlock.Text = $"Attendance Logs - {_employee.EmployeeNo}";
            SubHeaderTextBlock.Text = _employee.EmployeeName;
            SummaryTextBlock.Text = $"Employee: {_employee.EmployeeNo} - {_employee.EmployeeName} | Days shown: {_rows.Count}";
            LogsGrid.ItemsSource = _rows;
        }

        private void BackButton_OnClick(object sender, RoutedEventArgs e) => Close();

        private void ViewPdfButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var pdfPath = GeneratePdfToTempFile();
                Process.Start(new ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to open PDF: {ex.Message}", "Employee Attendance Logs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var pdfPath = GeneratePdfToTempFile();
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        Verb = "print",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pdfPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to print PDF: {ex.Message}", "Employee Attendance Logs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Employee Attendance Logs",
                    FileName = $"EmployeeAttendanceLogs-{_employee.EmployeeNo}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
                    DefaultExt = ".csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine("Date,Morning In,Morning Out,Afternoon In,Afternoon Out,Punches,Notes");
                foreach (var row in _rows)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(row.WorkDateText),
                        Csv(row.MorningIn),
                        Csv(row.MorningOut),
                        Csv(row.AfternoonIn),
                        Csv(row.AfternoonOut),
                        Csv(row.TotalPunches.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.Notes)));
                }

                await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to export CSV: {ex.Message}", "Employee Attendance Logs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GeneratePdfToTempFile()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var path = Path.Combine(
                Path.GetTempPath(),
                $"HRMS-EmployeeAttendanceLogs-{_employee.EmployeeNo}-{DateTime.Now:yyyyMMddHHmmss}.pdf");

            BuildPdf().GeneratePdf(path);
            return path;
        }

        private QuestPDF.Infrastructure.IDocument BuildPdf()
        {
            var generatedAt = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt", CultureInfo.InvariantCulture);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(3);
                        column.Item().Text("EMPLOYEE ATTENDANCE LOGS").FontSize(18).Bold().FontColor(QColors.Blue.Darken3);
                        column.Item().Text($"{_employee.EmployeeNo} - {_employee.EmployeeName}").FontColor(QColors.Grey.Darken2);
                        column.Item().Text($"Generated: {generatedAt}").FontColor(QColors.Grey.Darken2);
                    });

                    page.Content().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(82);
                            columns.ConstantColumn(72);
                            columns.ConstantColumn(76);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(86);
                            columns.ConstantColumn(54);
                            columns.RelativeColumn(1.6f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Date");
                            header.Cell().Element(CellHeader).Text("Morning In");
                            header.Cell().Element(CellHeader).Text("Morning Out");
                            header.Cell().Element(CellHeader).Text("Afternoon In");
                            header.Cell().Element(CellHeader).Text("Afternoon Out");
                            header.Cell().Element(CellHeader).Text("Punches");
                            header.Cell().Element(CellHeader).Text("Notes");
                        });

                        foreach (var row in _rows)
                        {
                            table.Cell().Element(CellBody).Text(row.WorkDateText);
                            table.Cell().Element(CellBody).Text(row.MorningIn);
                            table.Cell().Element(CellBody).Text(row.MorningOut);
                            table.Cell().Element(CellBody).Text(row.AfternoonIn);
                            table.Cell().Element(CellBody).Text(row.AfternoonOut);
                            table.Cell().Element(CellBody).Text(row.TotalPunches.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(CellBody).Text(row.Notes);
                        }
                    });

                    page.Footer().AlignRight().Text($"Rows: {_rows.Count}");
                });
            });
        }

        private static QuestPDF.Infrastructure.IContainer CellHeader(QuestPDF.Infrastructure.IContainer container) =>
            container
                .Background("#EAF2FB")
                .BorderBottom(1)
                .BorderColor("#D5E3F5")
                .Padding(6)
                .DefaultTextStyle(x => x.SemiBold().FontColor(QColors.Blue.Darken3));

        private static QuestPDF.Infrastructure.IContainer CellBody(QuestPDF.Infrastructure.IContainer container) =>
            container
                .BorderBottom(1)
                .BorderColor("#E5EDF7")
                .Padding(6);

        private static string Csv(string? input)
        {
            var value = input ?? string.Empty;
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}

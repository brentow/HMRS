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
    public partial class EmployeeDtrWindow : Window
    {
        private readonly DtrEmployeeSummaryVm _employee;
        private readonly int _year;
        private readonly int _month;
        private readonly List<DtrDailyRowVm> _rows;

        public EmployeeDtrWindow(
            DtrEmployeeSummaryVm employee,
            int year,
            int month,
            IReadOnlyList<DtrDailyRowVm> rows)
        {
            InitializeComponent();

            _employee = employee;
            _year = year;
            _month = month;
            _rows = rows?.ToList() ?? new List<DtrDailyRowVm>();

            var monthLabel = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(_month);
            Title = $"DTR - {_employee.EmployeeNo}";
            HeaderTextBlock.Text = $"DTR - {_employee.EmployeeNo}";
            SubHeaderTextBlock.Text = $"{_employee.EmployeeName} | {monthLabel} {_year}";
            SummaryTextBlock.Text = $"Employee: {_employee.EmployeeNo} - {_employee.EmployeeName} | Present: {_employee.PresentDays} | Leave: {_employee.LeaveDays} | Absent: {_employee.AbsentDays} | Late days: {_employee.LateDays} | Undertime: {_employee.UndertimeText} | Overtime: {_employee.OvertimeText} | Worked: {_employee.WorkedHoursText} | Attendance deduction: {_employee.AttendanceDeductionText}";
            DtrGrid.ItemsSource = _rows;
        }

        private void BackButton_OnClick(object sender, RoutedEventArgs e) => Close();

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
                MessageBox.Show(this, $"Unable to print DTR: {ex.Message}", "Employee DTR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DownloadButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Download DTR",
                    FileName = $"DTR-CS-Form-48-{_employee.EmployeeNo}-{_year}-{_month:00}.pdf",
                    DefaultExt = ".pdf",
                    Filter = "PDF files (*.pdf)|*.pdf|CSV files (*.csv)|*.csv",
                    FilterIndex = 1,
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return;
                }

                if (dialog.FilterIndex == 1 || string.Equals(Path.GetExtension(dialog.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    BuildPdf().GeneratePdf(dialog.FileName);
                    return;
                }

                var builder = new StringBuilder();
                builder.AppendLine("Employee No,Employee Name,Date,Day,AM Arrival,AM Departure,PM Arrival,PM Departure,Scheduled Minutes,Worked Minutes,Worked Hours,Late Minutes,Undertime Minutes,Overtime Minutes,Status,Attendance Deduction,Remarks");

                foreach (var row in _rows)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(row.EmployeeNo),
                        Csv(row.EmployeeName),
                        Csv(row.DateText),
                        Csv(row.DayName),
                        Csv(row.AmArrival),
                        Csv(row.AmDeparture),
                        Csv(row.PmArrival),
                        Csv(row.PmDeparture),
                        Csv(row.ScheduledMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.WorkedMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.WorkedHoursText),
                        Csv(row.LateMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.EarlyOutMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.OvertimeMinutes.ToString(CultureInfo.InvariantCulture)),
                        Csv(row.StatusDisplay),
                        Csv(row.AttendanceDeduction.ToString("0.00", CultureInfo.InvariantCulture)),
                        Csv(row.Remarks)));
                }

                await File.WriteAllTextAsync(dialog.FileName, builder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to download DTR: {ex.Message}", "Employee DTR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OpenPdfPreview()
        {
            var path = GeneratePdfToTempFile();
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        private string GeneratePdfToTempFile()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var path = Path.Combine(
                Path.GetTempPath(),
                $"HRMS-DTR-{_employee.EmployeeNo}-{_year}-{_month:00}-{DateTime.Now:yyyyMMddHHmmss}.pdf");

            BuildPdf().GeneratePdf(path);
            return path;
        }

        private QuestPDF.Infrastructure.IDocument BuildPdf()
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(18);
                    page.MarginVertical(20);
                    page.DefaultTextStyle(x => x.FontSize(6.4f).FontFamily("Arial").FontColor(QColors.Black));

                    page.Content().Row(copies =>
                    {
                        copies.RelativeItem().Element(ComposeCivilServiceForm48);
                        copies.ConstantItem(12).Text(string.Empty);
                        copies.RelativeItem().Element(ComposeCivilServiceForm48);
                    });
                });
            });
        }

        private void ComposeCivilServiceForm48(QuestPDF.Infrastructure.IContainer container)
        {
            var monthLabel = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(_month);
            var daysInMonth = DateTime.DaysInMonth(_year, _month);
            var rowsByDay = _rows
                .Where(x => x.WorkDate.Year == _year && x.WorkDate.Month == _month)
                .GroupBy(x => x.WorkDate.Day)
                .ToDictionary(x => x.Key, x => x.First());

            container.Column(column =>
            {
                column.Spacing(2);
                column.Item().AlignCenter().Text("Civil Service Form No. 48").FontSize(7.2f).Italic();
                column.Item().AlignCenter().Text("DAILY TIME RECORD").FontSize(11).Bold();
                column.Item().AlignCenter().Text("-----o0o-----").FontSize(6.5f);

                column.Item().PaddingTop(7).BorderBottom(0.8f).PaddingBottom(2).AlignCenter()
                    .Text(_employee.EmployeeName).FontSize(7.5f).SemiBold();
                column.Item().AlignCenter().Text("(Name)").FontSize(6.4f);

                column.Item().PaddingTop(4).Table(info =>
                {
                    info.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(73);
                        columns.RelativeColumn();
                    });

                    AddFormInfoRow(info, "For the month of", $"{monthLabel} 1 - {daysInMonth}, {_year}");
                    AddFormInfoRow(info, "Official hours for", "Regular days   8:00 AM - 5:00 PM");
                    AddFormInfoRow(info, "arrival and departure", "Saturdays      -");
                });

                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(21);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.ConstantColumn(32);
                        columns.ConstantColumn(34);
                    });

                    table.Header(header =>
                    {
                        header.Cell().RowSpan(2).Element(CivilHeaderCell).Text("Day");
                        header.Cell().ColumnSpan(2).Element(CivilHeaderCell).Text("A.M.");
                        header.Cell().ColumnSpan(2).Element(CivilHeaderCell).Text("P.M.");
                        header.Cell().ColumnSpan(2).Element(CivilHeaderCell).Text("Undertime");
                        header.Cell().Element(CivilHeaderCell).Text("Arrival");
                        header.Cell().Element(CivilHeaderCell).Text("Departure");
                        header.Cell().Element(CivilHeaderCell).Text("Arrival");
                        header.Cell().Element(CivilHeaderCell).Text("Departure");
                        header.Cell().Element(CivilHeaderCell).Text("Hours");
                        header.Cell().Element(CivilHeaderCell).Text("Minutes");
                    });

                    for (var day = 1; day <= 31; day++)
                    {
                        rowsByDay.TryGetValue(day, out var row);
                        table.Cell().Element(CivilDayCell).Text(day.ToString(CultureInfo.InvariantCulture)).Bold();

                        if (day > daysInMonth)
                        {
                            for (var cell = 0; cell < 6; cell++)
                            {
                                table.Cell().Element(CivilBodyCell).Text(string.Empty);
                            }
                            continue;
                        }

                        var dayLabel = GetCivilServiceDayLabel(row);
                        if (!string.IsNullOrWhiteSpace(dayLabel))
                        {
                            table.Cell().ColumnSpan(4).Element(CivilStatusCell).Text(SpreadLabel(dayLabel));
                            table.Cell().Element(CivilBodyCell).Text(string.Empty);
                            table.Cell().Element(CivilBodyCell).Text(string.Empty);
                            continue;
                        }

                        table.Cell().Element(CivilBodyCell).Text(FormatCivilServiceTime(row?.TimeIn));
                        table.Cell().Element(CivilBodyCell).Text(row?.TimeIn.HasValue == true && row.TimeOut.HasValue ? "12:00" : string.Empty);
                        table.Cell().Element(CivilBodyCell).Text(row?.TimeIn.HasValue == true && row.TimeOut.HasValue ? "01:00" : string.Empty);
                        table.Cell().Element(CivilBodyCell).Text(FormatCivilServiceTime(row?.TimeOut));
                        table.Cell().Element(CivilBodyCell).Text(row is { EarlyOutMinutes: >= 60 } ? (row.EarlyOutMinutes / 60).ToString(CultureInfo.InvariantCulture) : string.Empty);
                        table.Cell().Element(CivilBodyCell).Text(row is { EarlyOutMinutes: > 0 } ? (row.EarlyOutMinutes % 60).ToString(CultureInfo.InvariantCulture) : string.Empty);
                    }
                });

                column.Item().PaddingTop(8).Text(
                        "I certify on my honor that the above is a true and correct report of the hours of work performed, record of which was made daily at the time of arrival and departure from office.")
                    .FontSize(5.8f).Italic().LineHeight(1.15f);

                column.Item().PaddingTop(18).BorderBottom(0.8f).PaddingBottom(2).AlignCenter()
                    .Text(_employee.EmployeeName.ToUpperInvariant()).FontSize(6.8f).SemiBold();

                column.Item().PaddingTop(12).Text("VERIFIED as to the prescribed office hours:").FontSize(6.1f);
                column.Item().PaddingTop(20).BorderBottom(0.8f).Text(string.Empty);
                column.Item().AlignCenter().PaddingTop(2).Text("In Charge").FontSize(6f);
            });
        }

        private static void AddFormInfoRow(TableDescriptor table, string label, string value)
        {
            table.Cell().PaddingVertical(1).Text(label).Italic();
            table.Cell().BorderBottom(0.7f).PaddingHorizontal(3).PaddingVertical(1).Text(value).SemiBold();
        }

        private static QuestPDF.Infrastructure.IContainer CivilHeaderCell(QuestPDF.Infrastructure.IContainer container) =>
            container
                .Border(0.7f)
                .BorderColor(QColors.Black)
                .MinHeight(15)
                .PaddingHorizontal(1)
                .PaddingVertical(2)
                .AlignCenter()
                .AlignMiddle()
                .DefaultTextStyle(x => x.FontSize(5.8f).SemiBold());

        private static QuestPDF.Infrastructure.IContainer CivilBodyCell(QuestPDF.Infrastructure.IContainer container) =>
            container
                .Border(0.55f)
                .BorderColor(QColors.Black)
                .Height(13.2f)
                .PaddingHorizontal(1)
                .AlignCenter()
                .AlignMiddle()
                .DefaultTextStyle(x => x.FontSize(5.7f));

        private static QuestPDF.Infrastructure.IContainer CivilDayCell(QuestPDF.Infrastructure.IContainer container) =>
            CivilBodyCell(container).DefaultTextStyle(x => x.FontSize(5.9f).SemiBold());

        private static QuestPDF.Infrastructure.IContainer CivilStatusCell(QuestPDF.Infrastructure.IContainer container) =>
            CivilBodyCell(container).DefaultTextStyle(x => x.FontSize(5.4f).Italic().SemiBold());

        private static string FormatCivilServiceTime(DateTime? value) =>
            value.HasValue ? value.Value.ToString("hh:mm", CultureInfo.InvariantCulture) : string.Empty;

        private static string GetCivilServiceDayLabel(DtrDailyRowVm? row)
        {
            if (row == null || string.Equals(row.StatusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var remarks = row.Remarks ?? string.Empty;
            if (row.StatusCode is "TRAVEL_ORDER" or "OFFICIAL_BUSINESS" ||
                remarks.Contains("TRAVEL", StringComparison.OrdinalIgnoreCase))
            {
                return "TRAVEL ORDER";
            }

            return row.StatusCode switch
            {
                "WEEKEND" => row.WorkDate.DayOfWeek == DayOfWeek.Saturday ? "SATURDAY" : "SUNDAY",
                "HOLIDAY" => "HOLIDAY",
                "LEAVE" => "LEAVE",
                "ABSENT" => "ABSENT",
                _ => string.Empty
            };
        }

        private static string SpreadLabel(string label) =>
            string.Join(" ", label.ToUpperInvariant().ToCharArray());

        private static string Csv(string? input)
        {
            var value = input ?? string.Empty;
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}

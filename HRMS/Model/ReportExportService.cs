using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColors = QuestPDF.Helpers.Colors;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HRMS.Model
{
    public sealed class ReportExportService
    {
        private const string SystemBrand = "ePRIME+";
        private const string SystemSubtitle = "Human Resource Management System";
        private const string PrimaryHex = "#2B6CB0";
        private const string HeaderFillHex = "#2F7D32";
        private const string LightRowHex = "#F5F8FC";
        private const string DividerHex = "#2D9CFF";
        private const string MutedTextHex = "#8A94A6";

        public Task ExportPdfAsync(ReportDataset dataset, CompanyProfile profile, string filePath)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            ArgumentNullException.ThrowIfNull(profile);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            QuestPDF.Settings.License = LicenseType.Community;
            var logoBytes = TryResolveLogoBytes(profile.LogoPath);
            var document = BuildPdf(dataset, profile, logoBytes);
            document.GeneratePdf(filePath);
            return Task.CompletedTask;
        }

        public Task ExportExcelAsync(ReportDataset dataset, CompanyProfile profile, string filePath)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            ArgumentNullException.ThrowIfNull(profile);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Report");
            worksheet.Style.Font.FontName = "Calibri";
            worksheet.Style.Font.FontSize = 11;
            worksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var logoBytes = TryResolveLogoBytes(profile.LogoPath);
            var maxContentColumn = Math.Max(dataset.Table.Columns.Count, 8);
            var generatedText = $"Total: {dataset.Table.Rows.Count:N0} rows | Generated: {dataset.GeneratedAt:MMM dd, yyyy hh:mm tt}";
            var profileContext = BuildProfileContextText(profile, dataset);
            var filterContext = BuildFilterContextText(dataset);
            var textColumn = 2;
            if (logoBytes != null)
            {
                using var logoStream = new MemoryStream(logoBytes);
                var picture = worksheet.AddPicture(logoStream);
                picture.MoveTo(worksheet.Cell(1, 1));
                picture.WithSize(64, 64);
            }

            var companyCell = worksheet.Cell(1, textColumn);
            companyCell.Value = profile.CompanyName.ToUpperInvariant();
            companyCell.Style.Font.Bold = false;
            companyCell.Style.Font.FontColor = XLColor.FromHtml(MutedTextHex);
            companyCell.Style.Font.FontSize = 11;

            var brandCell = worksheet.Cell(2, textColumn);
            brandCell.Value = $"{SystemBrand}  {SystemSubtitle}";
            brandCell.Style.Font.Bold = true;
            brandCell.Style.Font.FontColor = XLColor.FromHtml(PrimaryHex);
            brandCell.Style.Font.FontSize = 12;

            var titleCell = worksheet.Cell(3, textColumn);
            titleCell.Value = dataset.ReportName;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontColor = XLColor.FromHtml("#2D3748");
            titleCell.Style.Font.FontSize = 18;

            var metaCell = worksheet.Cell(4, textColumn);
            metaCell.Value = generatedText;
            metaCell.Style.Font.FontColor = XLColor.FromHtml(MutedTextHex);
            metaCell.Style.Font.FontSize = 10;

            var profileCell = worksheet.Cell(5, textColumn);
            profileCell.Value = profileContext;
            profileCell.Style.Font.FontColor = XLColor.FromHtml(MutedTextHex);
            profileCell.Style.Font.FontSize = 9;

            var filterCell = worksheet.Cell(6, textColumn);
            filterCell.Value = filterContext;
            filterCell.Style.Font.FontColor = XLColor.FromHtml(MutedTextHex);
            filterCell.Style.Font.FontSize = 9;

            var headerTextRange = worksheet.Range(1, textColumn, 6, maxContentColumn);
            headerTextRange.Style.Alignment.WrapText = true;

            var dividerRow = 7;
            var dividerRange = worksheet.Range(dividerRow, 1, dividerRow, maxContentColumn);
            dividerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thick;
            dividerRange.Style.Border.BottomBorderColor = XLColor.FromHtml(DividerHex);

            var tableStartRow = 9;
            if (dataset.Table.Columns.Count > 0)
            {
                var dataRange = worksheet.Cell(tableStartRow, 1).InsertData(BuildExcelRows(dataset));
                dataRange.Style.Alignment.WrapText = true;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                var headerRange = worksheet.Range(tableStartRow, 1, tableStartRow, dataset.Table.Columns.Count);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderFillHex);
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                var bodyStartRow = tableStartRow + 1;
                var bodyEndRow = tableStartRow + dataset.Table.Rows.Count;
                if (bodyEndRow >= bodyStartRow)
                {
                    for (var row = bodyStartRow; row <= bodyEndRow; row++)
                    {
                        var range = worksheet.Range(row, 1, row, dataset.Table.Columns.Count);
                        range.Style.Fill.BackgroundColor = (row - bodyStartRow) % 2 == 0
                            ? XLColor.White
                            : XLColor.FromHtml(LightRowHex);
                    }
                }

                var wholeTable = worksheet.Range(tableStartRow, 1, Math.Max(tableStartRow, bodyEndRow), dataset.Table.Columns.Count);
                wholeTable.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                wholeTable.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D9E2EC");
                wholeTable.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                wholeTable.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
                wholeTable.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                wholeTable.Style.Border.BottomBorderColor = XLColor.FromHtml("#D9E2EC");
            }
            else
            {
                worksheet.Cell(tableStartRow, 1).Value = "No data to export.";
            }

            worksheet.Row(1).Height = 20;
            worksheet.Row(2).Height = 18;
            worksheet.Row(3).Height = 28;
            worksheet.Row(4).Height = 18;
            worksheet.Row(5).Height = 18;
            worksheet.Row(6).Height = 15;
            worksheet.Row(8).Height = 8;

            worksheet.Column(1).Width = Math.Max(14, worksheet.Column(1).Width);

            var maxColumns = Math.Max(1, dataset.Table.Columns.Count);
            for (var column = 1; column <= maxColumns; column++)
            {
                worksheet.Column(column).AdjustToContents();
                if (worksheet.Column(column).Width > 48)
                {
                    worksheet.Column(column).Width = 48;
                }
            }

            worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            worksheet.PageSetup.FitToPages(1, 0);
            worksheet.PageSetup.CenterHorizontally = true;
            worksheet.PageSetup.Margins.Top = 0.35;
            worksheet.PageSetup.Margins.Bottom = 0.35;
            worksheet.PageSetup.Margins.Left = 0.25;
            worksheet.PageSetup.Margins.Right = 0.25;
            workbook.SaveAs(filePath);
            return Task.CompletedTask;
        }

        private static IDocument BuildPdf(ReportDataset dataset, CompanyProfile profile, byte[]? logoBytes)
        {
            var filterContext = BuildFilterContextText(dataset);
            var generatedText = $"Total: {dataset.Table.Rows.Count:N0} rows | Generated: {dataset.GeneratedAt:MMM dd, yyyy hh:mm tt}";
            var profileContext = BuildProfileContextText(profile, dataset);
            var yearText = dataset.GeneratedAt.Year.ToString(CultureInfo.InvariantCulture);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Segoe UI"));

                    page.Header().Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            if (logoBytes != null)
                            {
                                row.ConstantItem(56).Element(logo =>
                                    logo.Width(48)
                                        .Height(48)
                                        .AlignLeft()
                                        .AlignTop()
                                        .Image(logoBytes)
                                        .FitArea());
                            }

                            row.RelativeItem().Column(text =>
                            {
                                text.Spacing(1);
                                text.Item().Text(profile.CompanyName.ToUpperInvariant()).FontSize(9).FontColor(MutedTextHex);
                                text.Item().Text(SystemBrand).FontSize(11).SemiBold().FontColor(PrimaryHex);
                                text.Item().Text(dataset.ReportName).FontSize(17).Bold().FontColor("#2D3748");
                                text.Item().Text(generatedText).FontSize(8.5f).FontColor(MutedTextHex);
                                text.Item().Text(profileContext).FontSize(8.2f).FontColor(MutedTextHex);
                            });

                            row.ConstantItem(72)
                                .AlignRight()
                                .AlignTop()
                                .Text(yearText)
                                .FontSize(22)
                                .Bold()
                                .FontColor("#E6E8EC");
                        });

                        column.Item().Text(filterContext).FontSize(8.2f).FontColor(MutedTextHex);
                        column.Item().PaddingTop(2).LineHorizontal(1.5f).LineColor(DividerHex);
                    });

                    page.Content().PaddingTop(8).Element(content =>
                    {
                        if (dataset.Table.Columns.Count == 0)
                        {
                            content.AlignCenter().PaddingVertical(20).Text("No data to export.").FontColor(QColors.Grey.Darken2);
                            return;
                        }

                        content.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (DataColumn _ in dataset.Table.Columns)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (DataColumn column in dataset.Table.Columns)
                                {
                                    header.Cell().Element(PdfHeaderCell).Text(column.ColumnName);
                                }
                            });

                            var rowIndex = 0;
                            foreach (DataRow row in dataset.Table.Rows)
                            {
                                foreach (DataColumn column in dataset.Table.Columns)
                                {
                                    var value = ToCellText(row[column]);
                                    table.Cell().Element(container => PdfBodyCell(container, rowIndex)).Text(value);
                                }

                                rowIndex++;
                            }
                        });
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().LineHorizontal(0.6f).LineColor("#E2E8F0");
                            column.Item().PaddingTop(4).Text($"{profile.CompanyName} | {profile.SerialNumber}").FontSize(7.5f).FontColor("#B0B7C3");
                        });
                        row.RelativeItem()
                            .AlignRight()
                            .PaddingTop(4)
                            .DefaultTextStyle(x => x.FontSize(7.5f).FontColor("#B0B7C3"))
                            .Text(text =>
                            {
                                text.Span("Page ");
                                text.CurrentPageNumber();
                                text.Span(" of ");
                                text.TotalPages();
                            });
                    });
                });
            });
        }

        private static IContainer PdfHeaderCell(IContainer container)
        {
            return container
                .Background(HeaderFillHex)
                .PaddingVertical(6)
                .PaddingHorizontal(5)
                .DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
        }

        private static IContainer PdfBodyCell(IContainer container, int rowIndex)
        {
            return container
                .Background(rowIndex % 2 == 0 ? Colors.White : LightRowHex)
                .PaddingVertical(5)
                .PaddingHorizontal(5);
        }

        private static string BuildFilterContextText(ReportDataset dataset)
        {
            if (dataset.DateFrom.HasValue && dataset.DateTo.HasValue)
            {
                return $"Date Range: {dataset.DateFrom.Value:yyyy-MM-dd} to {dataset.DateTo.Value:yyyy-MM-dd}";
            }

            if (dataset.DateFrom.HasValue)
            {
                return $"Date From: {dataset.DateFrom.Value:yyyy-MM-dd}";
            }

            if (dataset.DateTo.HasValue)
            {
                return $"Date To: {dataset.DateTo.Value:yyyy-MM-dd}";
            }

            return "Date Range: All";
        }

        private static string BuildProfileContextText(CompanyProfile profile, ReportDataset dataset)
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(profile.Address) ? null : profile.Address.Trim(),
                string.IsNullOrWhiteSpace(profile.OwnerName) ? null : $"Owner: {profile.OwnerName.Trim()}",
                string.IsNullOrWhiteSpace(profile.SerialNumber) ? null : $"Serial: {profile.SerialNumber.Trim()}",
                string.IsNullOrWhiteSpace(dataset.CategoryName) ? null : $"Category: {dataset.CategoryName.Trim()}"
            };

            var context = parts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();

            return context.Length == 0
                ? $"{SystemBrand} {SystemSubtitle}"
                : string.Join("  |  ", context);
        }

        private static string ToCellText(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "-";
            }

            return value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                decimal number => number.ToString("N2", CultureInfo.InvariantCulture),
                double number => number.ToString("N2", CultureInfo.InvariantCulture),
                float number => number.ToString("N2", CultureInfo.InvariantCulture),
                _ => value.ToString()?.Trim() ?? "-"
            };
        }

        private static IEnumerable<IEnumerable<object?>> BuildExcelRows(ReportDataset dataset)
        {
            var rows = new List<IEnumerable<object?>>(capacity: dataset.Table.Rows.Count + 1)
            {
                dataset.Table.Columns.Cast<DataColumn>().Select(column => (object?)column.ColumnName).ToArray()
            };

            foreach (DataRow row in dataset.Table.Rows)
            {
                rows.Add(dataset.Table.Columns.Cast<DataColumn>().Select(column => row[column]).ToArray());
            }

            return rows;
        }

        private static byte[]? TryResolveLogoBytes(string? logoPath)
        {
            var candidates = BuildLogoPathCandidates(logoPath);
            foreach (var path in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
                }
                catch
                {
                    // Ignore unreadable logo paths and continue with next candidate.
                }
            }

            return TryLoadPackResourceLogo();
        }

        private static string[] BuildLogoPathCandidates(string? rawPath)
        {
            var list = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(rawPath))
            {
                var normalized = rawPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                if (Path.IsPathFullyQualified(normalized))
                {
                    list.Add(normalized);
                }
                else
                {
                    list.Add(Path.Combine(AppContext.BaseDirectory, normalized));
                }

                if (normalized.Contains("Images", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(Path.Combine(AppContext.BaseDirectory, "Images", Path.GetFileName(normalized)));
                }
            }

            list.Add(Path.Combine(AppContext.BaseDirectory, "Images", "ePRIME_logo.png"));
            list.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "ePRIME_logo.png"));
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static byte[]? TryLoadPackResourceLogo()
        {
            var uris = new[]
            {
                "pack://application:,,,/Images/ePRIME_logo.png",
                "pack://application:,,,/HRMS;component/Images/ePRIME_logo.png"
            };

            foreach (var rawUri in uris)
            {
                try
                {
                    var resourceInfo = Application.GetResourceStream(new Uri(rawUri, UriKind.Absolute));
                    if (resourceInfo?.Stream == null)
                    {
                        continue;
                    }

                    using var stream = resourceInfo.Stream;
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
                catch
                {
                    // Ignore invalid resource URIs and try next candidate.
                }
            }

            return null;
        }
    }
}

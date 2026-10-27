using System.Security.Cryptography;
using ClosedXML.Excel;
using PayrollAutomator.Application;
using PayrollAutomator.Domain;

namespace PayrollAutomator.Infrastructure;

public sealed class ClosedXmlPayrollWorkbookExporter : IPayrollWorkbookExporter
{
    public PayrollExportResult Export(PayrollExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.SourcePath))
        {
            throw new FileNotFoundException("The source workbook was not found.", request.SourcePath);
        }

        string? outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (File.Exists(request.OutputPath))
        {
            throw new IOException($"The output workbook already exists: {request.OutputPath}");
        }

        string sourceFingerprint = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(request.SourcePath)));
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string sourceFileName = Path.GetFileName(request.SourcePath);
        string outputFileName = Path.GetFileName(request.OutputPath);

        var auditEntries = new List<AuditLogEntry>
        {
            new(timestamp, "ReviewConfirmed", $"Reviewer '{request.Confirmation.Reviewer}' confirmed {request.Review.WarningCount} warning(s)."),
            new(timestamp, "WorkbookExported", $"Created '{outputFileName}' from '{sourceFileName}'.")
        };

        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "Payroll Automator export";
        workbook.Properties.Subject = "Reviewable payroll calculation output";

        WritePayrollSheet(workbook.AddWorksheet("Payroll"), request.Calculation);
        WriteEmployeeTotalsSheet(workbook.AddWorksheet("Employee Totals"), request.Calculation);
        WriteReviewSheet(workbook.AddWorksheet("Review"), request.Review);
        WriteMetadataSheet(
            workbook.AddWorksheet("Metadata"),
            request.Calculation,
            request.Review,
            request.Confirmation,
            sourceFileName,
            sourceFingerprint,
            outputFileName);
        WriteAuditSheet(workbook.AddWorksheet("Audit"), auditEntries);

        using (FileStream stream = new(
                   request.OutputPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            workbook.SaveAs(stream);
        }

        return new PayrollExportResult(request.OutputPath, auditEntries);
    }

    private static void WritePayrollSheet(
        IXLWorksheet worksheet,
        PayrollCalculationResult calculation)
    {
        string[] headers =
        [
            "Source Sheet",
            "Source Row",
            "Period",
            "Employee Number",
            "Employee Name",
            "Prorata",
            "Category",
            "Source Value",
            "Payroll Hours",
            "Payable Amount",
            "Configured",
            "Excel Time Value"
        ];
        WriteHeaders(worksheet, headers);

        int rowNumber = 2;
        foreach (CalculatedPayrollRow row in calculation.Rows)
        {
            foreach (CalculatedCategory category in row.Categories)
            {
                worksheet.Cell(rowNumber, 1).Value = row.SheetName;
                worksheet.Cell(rowNumber, 2).Value = row.SourceRowNumber;
                worksheet.Cell(rowNumber, 3).Value = row.PeriodLabel;
                worksheet.Cell(rowNumber, 4).Value = row.EmployeeNumber;
                worksheet.Cell(rowNumber, 5).Value = row.EmployeeName;
                worksheet.Cell(rowNumber, 6).Value = row.IsProrata;
                worksheet.Cell(rowNumber, 7).Value = category.Category.ToString();
                SetNullableDecimal(worksheet.Cell(rowNumber, 8), category.SourceValue);
                SetNullableDecimal(worksheet.Cell(rowNumber, 9), category.PayrollHours);
                SetNullableDecimal(worksheet.Cell(rowNumber, 10), category.PayableAmount);
                worksheet.Cell(rowNumber, 11).Value = category.IsConfigured;
                worksheet.Cell(rowNumber, 12).Value = category.IsExcelTimeValue;
                rowNumber++;
            }
        }

        ApplyCurrencyFormat(worksheet, 10, rowNumber - 1);
        ApplyDecimalFormat(worksheet, 8, rowNumber - 1);
        ApplyDecimalFormat(worksheet, 9, rowNumber - 1);
        FinishWorksheet(worksheet, headers.Length);
    }

    private static void WriteEmployeeTotalsSheet(
        IXLWorksheet worksheet,
        PayrollCalculationResult calculation)
    {
        string[] headers =
        [
            "Employee Number",
            "Employee Name",
            "Payroll Hours",
            "Payable Amount",
            "Included Rows",
            "Prorata Rows"
        ];
        WriteHeaders(worksheet, headers);

        int rowNumber = 2;
        foreach (EmployeePayrollTotal total in calculation.EmployeeTotals)
        {
            worksheet.Cell(rowNumber, 1).Value = total.EmployeeNumber;
            worksheet.Cell(rowNumber, 2).Value = total.EmployeeName;
            worksheet.Cell(rowNumber, 3).Value = total.PayrollHours;
            worksheet.Cell(rowNumber, 4).Value = total.PayableAmount;
            worksheet.Cell(rowNumber, 5).Value = total.IncludedRowCount;
            worksheet.Cell(rowNumber, 6).Value = total.ProrataRowCount;
            rowNumber++;
        }

        worksheet.Cell(rowNumber, 1).Value = "TOTAL";
        worksheet.Cell(rowNumber, 3).Value = calculation.EmployeeTotals.Sum(total => total.PayrollHours);
        worksheet.Cell(rowNumber, 4).Value = calculation.EmployeeTotals.Sum(total => total.PayableAmount);
        worksheet.Cell(rowNumber, 5).Value = calculation.EmployeeTotals.Sum(total => total.IncludedRowCount);
        worksheet.Cell(rowNumber, 6).Value = calculation.EmployeeTotals.Sum(total => total.ProrataRowCount);
        worksheet.Row(rowNumber).Style.Font.Bold = true;

        ApplyCurrencyFormat(worksheet, 4, rowNumber);
        ApplyDecimalFormat(worksheet, 3, rowNumber);
        FinishWorksheet(worksheet, headers.Length);
    }

    private static void WriteReviewSheet(IXLWorksheet worksheet, PayrollReviewSummary review)
    {
        worksheet.Cell("A1").Value = "Payroll Review";
        worksheet.Cell("A1").Style.Font.Bold = true;
        worksheet.Cell("A3").Value = "Calculated rows";
        worksheet.Cell("B3").Value = review.CalculatedRowCount;
        worksheet.Cell("A4").Value = "Complete rows";
        worksheet.Cell("B4").Value = review.CompleteRowCount;
        worksheet.Cell("A5").Value = "Incomplete rows";
        worksheet.Cell("B5").Value = review.IncompleteRowCount;
        worksheet.Cell("A6").Value = "Prorata rows";
        worksheet.Cell("B6").Value = review.ProrataRowCount;
        worksheet.Cell("A7").Value = "Errors";
        worksheet.Cell("B7").Value = review.ErrorCount;
        worksheet.Cell("A8").Value = "Warnings";
        worksheet.Cell("B8").Value = review.WarningCount;

        string[] headers =
        ["Severity", "Code", "Message", "Sheet", "Source Row", "Cell", "Category"];
        WriteHeaders(worksheet, headers, 10);

        int rowNumber = 11;
        foreach (PayrollReviewException exception in review.Exceptions)
        {
            worksheet.Cell(rowNumber, 1).Value = exception.Severity.ToString();
            worksheet.Cell(rowNumber, 2).Value = exception.Code;
            worksheet.Cell(rowNumber, 3).Value = exception.Message;
            worksheet.Cell(rowNumber, 4).Value = exception.SheetName;
            SetNullableInt(worksheet.Cell(rowNumber, 5), exception.SourceRowNumber);
            worksheet.Cell(rowNumber, 6).Value = exception.CellAddress ?? string.Empty;
            worksheet.Cell(rowNumber, 7).Value = exception.Category?.ToString() ?? string.Empty;
            rowNumber++;
        }

        worksheet.Column(3).Width = 70;
        worksheet.Column(3).Style.Alignment.WrapText = true;
    }

    private static void WriteMetadataSheet(
        IXLWorksheet worksheet,
        PayrollCalculationResult calculation,
        PayrollReviewSummary review,
        ReviewConfirmation confirmation,
        string sourceFileName,
        string sourceFingerprint,
        string outputFileName)
    {
        worksheet.Cell("A1").Value = "Payroll Automator metadata";
        worksheet.Cell("A1").Style.Font.Bold = true;
        string[,] metadata =
        {
            { "Application Version", typeof(PayrollApplication).Assembly.GetName().Version?.ToString() ?? "unversioned" },
            { "Rule Set Version", calculation.Metadata.RuleSetVersion },
            { "Hourly Rate", calculation.Metadata.HourlyRate.ToString("0.00") },
            { "Source Workbook", sourceFileName },
            { "Source SHA-256", sourceFingerprint },
            { "Output Workbook", outputFileName },
            { "Reviewer", confirmation.Reviewer },
            { "Confirmed UTC", confirmation.ConfirmedAtUtc.ToString("O") },
            { "Acknowledged Warnings", confirmation.AcknowledgedWarningCount.ToString() },
            { "Review Errors", review.ErrorCount.ToString() },
            { "Review Warnings", review.WarningCount.ToString() }
        };

        for (int row = 0; row < metadata.GetLength(0); row++)
        {
            worksheet.Cell(row + 3, 1).Value = metadata[row, 0];
            worksheet.Cell(row + 3, 2).Value = metadata[row, 1];
        }

        WriteHeaders(worksheet, ["Category", "Multiplier"], 16);
        int multiplierRow = 17;
        foreach (PayrollCategory category in Enum.GetValues<PayrollCategory>())
        {
            worksheet.Cell(multiplierRow, 1).Value = category.ToString();
            if (calculation.Metadata.CategoryMultipliers.TryGetValue(category, out decimal multiplier))
            {
                worksheet.Cell(multiplierRow, 2).Value = multiplier;
            }
            else
            {
                worksheet.Cell(multiplierRow, 2).Value = "Not configured";
            }

            multiplierRow++;
        }

        worksheet.Column(1).Width = 22;
        worksheet.Column(2).Width = 30;
    }

    private static void WriteAuditSheet(
        IXLWorksheet worksheet,
        IReadOnlyList<AuditLogEntry> entries)
    {
        WriteHeaders(worksheet, ["Timestamp UTC", "Event", "Details"]);
        int rowNumber = 2;
        foreach (AuditLogEntry entry in entries)
        {
            worksheet.Cell(rowNumber, 1).Value = entry.TimestampUtc.ToString("O");
            worksheet.Cell(rowNumber, 2).Value = entry.EventType;
            worksheet.Cell(rowNumber, 3).Value = entry.Details;
            rowNumber++;
        }

        worksheet.Column(3).Width = 80;
        worksheet.Column(3).Style.Alignment.WrapText = true;
    }

    private static void WriteHeaders(
        IXLWorksheet worksheet,
        IReadOnlyList<string> headers,
        int rowNumber = 1)
    {
        for (int column = 0; column < headers.Count; column++)
        {
            worksheet.Cell(rowNumber, column + 1).Value = headers[column];
        }

        worksheet.Row(rowNumber).Style.Font.Bold = true;
        worksheet.Row(rowNumber).Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private static void FinishWorksheet(IXLWorksheet worksheet, int columnCount)
    {
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, 1, columnCount).SetAutoFilter();
    }

    private static void SetNullableDecimal(IXLCell cell, decimal? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
        }
    }

    private static void SetNullableInt(IXLCell cell, int? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
        }
    }

    private static void ApplyDecimalFormat(IXLWorksheet worksheet, int column, int lastRow)
    {
        if (lastRow >= 2)
        {
            worksheet.Range(2, column, lastRow, column).Style.NumberFormat.Format = "0.00";
        }
    }

    private static void ApplyCurrencyFormat(IXLWorksheet worksheet, int column, int lastRow)
    {
        if (lastRow >= 2)
        {
            worksheet.Range(2, column, lastRow, column).Style.NumberFormat.Format = "R #,##0.00";
        }
    }
}

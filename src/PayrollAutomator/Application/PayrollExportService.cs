namespace PayrollAutomator.Application;

public interface IPayrollWorkbookExporter
{
    PayrollExportResult Export(PayrollExportRequest request);
}

public sealed class PayrollExportService(IPayrollWorkbookExporter exporter)
{
    public PayrollExportResult Export(
        string sourcePath,
        string outputPath,
        PayrollCalculationResult calculation,
        PayrollReviewSummary review,
        ReviewConfirmation confirmation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(calculation);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(confirmation);

        if (review.HasErrors || calculation.HasErrors)
        {
            throw new InvalidOperationException(
                "Payroll cannot be exported while the review contains errors.");
        }

        string fullSourcePath = Path.GetFullPath(sourcePath);
        string fullOutputPath = Path.GetFullPath(outputPath);
        if (string.Equals(fullSourcePath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The output workbook must be different from the source workbook.");
        }

        decimal calculatedRowAmount = calculation.Rows
            .Where(row => row.TotalPayableAmount.HasValue)
            .Sum(row => row.TotalPayableAmount!.Value);
        decimal employeeTotalAmount = calculation.EmployeeTotals.Sum(total => total.PayableAmount);
        if (calculatedRowAmount != employeeTotalAmount ||
            review.IncludedPayableAmount != employeeTotalAmount)
        {
            throw new InvalidOperationException(
                "Calculation report totals do not reconcile and cannot be exported.");
        }

        return exporter.Export(new PayrollExportRequest(
            fullSourcePath,
            fullOutputPath,
            calculation,
            review,
            confirmation));
    }
}

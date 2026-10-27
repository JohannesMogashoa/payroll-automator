using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public sealed class PayrollApplication(
    IWorkbookImporter workbookImporter,
    IPayrollWorkbookExporter? workbookExporter = null)
{
    public WorkbookImportResult ImportWorkbook(string sourcePath) => workbookImporter.Import(sourcePath);

    public PayrollCalculationResult CalculateWorkbook(
        WorkbookImportResult importResult,
        PayrollRatePolicy? ratePolicy = null) =>
        new PayrollCalculationService(ratePolicy ?? PayrollRatePolicy.Default())
            .Calculate(importResult);

    public PayrollReviewSummary ReviewCalculation(PayrollCalculationResult calculation) =>
        new PayrollReviewService().Summarize(calculation);

    public ReviewConfirmation ConfirmReview(
        PayrollReviewSummary review,
        string reviewer,
        string? comment = null,
        DateTimeOffset? confirmedAtUtc = null) =>
        new PayrollReviewService().Confirm(review, reviewer, comment, confirmedAtUtc);

    public PayrollExportResult ExportWorkbook(
        string sourcePath,
        string outputPath,
        PayrollCalculationResult calculation,
        PayrollReviewSummary review,
        ReviewConfirmation confirmation)
    {
        if (workbookExporter is null)
        {
            throw new InvalidOperationException("No payroll workbook exporter has been configured.");
        }

        return new PayrollExportService(workbookExporter)
            .Export(sourcePath, outputPath, calculation, review, confirmation);
    }

    public string GetStatus() => "Workbook import, calculation, review and export are ready.";
}

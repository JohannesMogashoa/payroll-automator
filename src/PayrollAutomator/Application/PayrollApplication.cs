using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public sealed class PayrollApplication(IWorkbookImporter workbookImporter)
{
    public WorkbookImportResult ImportWorkbook(string sourcePath) => workbookImporter.Import(sourcePath);

    public PayrollCalculationResult CalculateWorkbook(
        WorkbookImportResult importResult,
        PayrollRatePolicy? ratePolicy = null) =>
        new PayrollCalculationService(ratePolicy ?? PayrollRatePolicy.Default())
            .Calculate(importResult);

    public string GetStatus() => "Workbook import and calculation are ready; review and export remain for later phases.";
}

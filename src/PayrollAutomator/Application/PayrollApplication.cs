namespace PayrollAutomator.Application;

public sealed class PayrollApplication(IWorkbookImporter workbookImporter)
{
    public WorkbookImportResult ImportWorkbook(string sourcePath) => workbookImporter.Import(sourcePath);

    public string GetStatus() => "Workbook import is ready; calculation and export workflows remain for later phases.";
}

using PayrollAutomator.Application;
using PayrollAutomator.Domain;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class PayrollCalculationServiceTests
{
    [Fact]
    public void Calculate_ProducesRowAndEmployeeTotalsWithSourceTraceability()
    {
        ImportedPayrollRow row = CreateRow(
            normal: new PayrollCellValue(8.5m, "General"));
        PayrollCalculationService service = new(PayrollRatePolicy.Default());

        PayrollCalculationResult result = service.Calculate(
            new WorkbookImportResult([row], []));

        CalculatedPayrollRow calculatedRow = Assert.Single(result.Rows);
        Assert.Equal("Weekly", calculatedRow.SheetName);
        Assert.Equal(7, calculatedRow.SourceRowNumber);
        Assert.Equal(8.5m, calculatedRow.TotalPayrollHours);
        Assert.Equal(1657.50m, calculatedRow.TotalPayableAmount);
        Assert.Empty(result.Issues);

        EmployeePayrollTotal employeeTotal = Assert.Single(result.EmployeeTotals);
        Assert.Equal("EMP-001", employeeTotal.EmployeeNumber);
        Assert.Equal(8.5m, employeeTotal.PayrollHours);
        Assert.Equal(1657.50m, employeeTotal.PayableAmount);
        Assert.Equal(1, employeeTotal.IncludedRowCount);
    }

    [Fact]
    public void Calculate_UsesExplicitCategoryMultipliers()
    {
        ImportedPayrollRow row = CreateRow(
            normal: new PayrollCellValue(8m, "General"),
            overtime: new PayrollCellValue(0.5m, "General"));
        var policy = new PayrollRatePolicy(
            195m,
            new Dictionary<PayrollCategory, decimal>
            {
                [PayrollCategory.Normal] = 1m,
                [PayrollCategory.Overtime] = 1.5m
            });

        PayrollCalculationResult result = new PayrollCalculationService(policy).Calculate(
            new WorkbookImportResult([row], []));

        CalculatedPayrollRow calculatedRow = Assert.Single(result.Rows);
        Assert.Equal(8.5m, calculatedRow.TotalPayrollHours);
        Assert.Equal(1706.25m, calculatedRow.TotalPayableAmount);
        Assert.Equal(146.25m, calculatedRow.Categories
            .Single(category => category.Category == PayrollCategory.Overtime)
            .PayableAmount);
    }

    [Fact]
    public void Calculate_ReportsUnconfiguredPresentCategoryWithoutCalculatingItsAmount()
    {
        ImportedPayrollRow row = CreateRow(
            normal: new PayrollCellValue(8m, "General"),
            overtime: new PayrollCellValue(1m, "General"));

        PayrollCalculationResult result = new PayrollCalculationService(
            new PayrollRatePolicy(195m, new Dictionary<PayrollCategory, decimal>
            {
                [PayrollCategory.Normal] = 1m
            }))
            .Calculate(new WorkbookImportResult([row], []));

        CalculatedPayrollRow calculatedRow = Assert.Single(result.Rows);
        Assert.Null(calculatedRow.TotalPayrollHours);
        Assert.Null(calculatedRow.TotalPayableAmount);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "UnconfiguredCategoryRate" &&
            issue.Category == PayrollCategory.Overtime);
    }

    [Fact]
    public void Calculate_DoesNotRequireAConfiguredRateForZeroCategories()
    {
        ImportedPayrollRow row = CreateRow(
            normal: new PayrollCellValue(8m, "General"),
            overtime: new PayrollCellValue(0m, "General"));

        PayrollCalculationResult result = new PayrollCalculationService(
            PayrollRatePolicy.Default())
            .Calculate(new WorkbookImportResult([row], []));

        Assert.False(result.HasErrors);
        Assert.Empty(result.Issues);
        Assert.Equal(1560m, Assert.Single(result.Rows).TotalPayableAmount);
    }

    [Fact]
    public void Calculate_PreservesProrataRowsAndExcludesThemFromPayableTotals()
    {
        ImportedPayrollRow row = CreateRow(
            normal: new PayrollCellValue(-0.5m, "General"));
        PayrollCalculationResult result = new PayrollCalculationService(
            new PayrollRatePolicy(195m, new Dictionary<PayrollCategory, decimal>
            {
                [PayrollCategory.Normal] = 1m
            }))
            .Calculate(new WorkbookImportResult([row], []));

        CalculatedPayrollRow calculatedRow = Assert.Single(result.Rows);
        Assert.True(calculatedRow.IsProrata);
        Assert.Null(calculatedRow.TotalPayrollHours);
        Assert.Null(calculatedRow.TotalPayableAmount);
        Assert.Equal(-0.5m, calculatedRow.Categories
            .Single(category => category.Category == PayrollCategory.Normal)
            .SourceValue);
        Assert.Contains(result.Issues, issue => issue.Code == "ProrataRequiresRoster");
        Assert.Equal(0, Assert.Single(result.EmployeeTotals).IncludedRowCount);
        Assert.Equal(1, Assert.Single(result.EmployeeTotals).ProrataRowCount);
    }

    private static ImportedPayrollRow CreateRow(
        PayrollCellValue? normal = null,
        PayrollCellValue? overtime = null)
    {
        return new ImportedPayrollRow(
            "Weekly",
            7,
            "2026-10-05 - 2026-10-11",
            new DateOnly(2026, 10, 5),
            new DateOnly(2026, 10, 11),
            "EMP-001",
            "Fictional Employee",
            normal ?? new PayrollCellValue(null, null),
            overtime ?? new PayrollCellValue(null, null),
            new PayrollCellValue(null, null),
            new PayrollCellValue(null, null),
            new PayrollCellValue(null, null),
            new PayrollCellValue(null, null),
            null);
    }
}

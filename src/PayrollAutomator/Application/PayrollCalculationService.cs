using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public sealed class PayrollCalculationService(PayrollRatePolicy ratePolicy)
{
    private static readonly (PayrollCategory Category, Func<ImportedPayrollRow, PayrollCellValue> ValueSelector)[] Categories =
    [
        (PayrollCategory.Normal, row => row.Normal),
        (PayrollCategory.Overtime, row => row.Overtime),
        (PayrollCategory.Sunday, row => row.Sunday),
        (PayrollCategory.PublicHoliday, row => row.PublicHoliday),
        (PayrollCategory.NightShift, row => row.NightShift),
        (PayrollCategory.SickLeave, row => row.SickLeave)
    ];

    public PayrollCalculationResult Calculate(WorkbookImportResult importResult)
    {
        ArgumentNullException.ThrowIfNull(importResult);

        var calculatedRows = new List<CalculatedPayrollRow>();
        var issues = new List<PayrollCalculationIssue>();

        foreach (ImportedPayrollRow importedRow in importResult.Rows)
        {
            CalculateRow(importedRow, calculatedRows, issues);
        }

        IReadOnlyList<EmployeePayrollTotal> employeeTotals = BuildEmployeeTotals(
            calculatedRows,
            issues);

        return new PayrollCalculationResult(
            calculatedRows,
            employeeTotals,
            importResult.Issues,
            issues,
            new PayrollCalculationMetadata(
                PayrollRules.RuleSetVersion,
                ratePolicy.HourlyRate,
                ratePolicy.Multipliers));
    }

    private void CalculateRow(
        ImportedPayrollRow importedRow,
        ICollection<CalculatedPayrollRow> calculatedRows,
        ICollection<PayrollCalculationIssue> issues)
    {
        var categories = new List<CalculatedCategory>(Categories.Length);
        bool isProrata = Categories.Any(pair => pair.ValueSelector(importedRow).RawValue < 0m);

        if (isProrata)
        {
            issues.Add(new PayrollCalculationIssue(
                CalculationIssueSeverity.Warning,
                "ProrataRequiresRoster",
                "Negative source values identify a Prorata row; roster hours are required before payable hours can be calculated.",
                importedRow.SheetName,
                importedRow.SourceRowNumber));
        }

        foreach ((PayrollCategory category, Func<ImportedPayrollRow, PayrollCellValue> selector) in Categories)
        {
            PayrollCellValue source = selector(importedRow);
            categories.Add(CalculateCategory(
                importedRow,
                category,
                source,
                isProrata,
                issues));
        }

        decimal? totalHours = null;
        decimal? totalAmount = null;

        if (!isProrata && categories.All(category =>
                category.PayrollHours.HasValue && category.PayableAmount.HasValue))
        {
            totalHours = categories.Sum(category => category.PayrollHours!.Value);
            totalAmount = PayrollRules.RoundCurrency(
                categories.Sum(category => category.PayableAmount!.Value));
        }

        calculatedRows.Add(new CalculatedPayrollRow(
            importedRow.SheetName,
            importedRow.SourceRowNumber,
            importedRow.PeriodLabel,
            importedRow.EmployeeNumber,
            importedRow.EmployeeName,
            categories,
            totalHours,
            totalAmount,
            isProrata));
    }

    private CalculatedCategory CalculateCategory(
        ImportedPayrollRow importedRow,
        PayrollCategory category,
        PayrollCellValue source,
        bool isProrata,
        ICollection<PayrollCalculationIssue> issues)
    {
        if (!source.RawValue.HasValue)
        {
            return new CalculatedCategory(category, null, 0m, 0m, isProrata, true, false);
        }

        decimal sourceValue = source.RawValue.Value;
        if (sourceValue < 0m)
        {
            return new CalculatedCategory(category, sourceValue, null, null, true, false, false);
        }

        NormalizedPayrollValue normalized;
        try
        {
            normalized = PayrollValueNormalizer.Normalize(sourceValue, source.NumberFormat);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or FormatException or OverflowException)
        {
            issues.Add(new PayrollCalculationIssue(
                CalculationIssueSeverity.Error,
                "InvalidPayrollTimeValue",
                exception.Message,
                importedRow.SheetName,
                importedRow.SourceRowNumber,
                category));

            return new CalculatedCategory(category, sourceValue, null, null, isProrata, false, false);
        }

        if (normalized.PayrollHours == 0m && !ratePolicy.TryGetMultiplier(category, out _))
        {
            return new CalculatedCategory(
                category,
                sourceValue,
                0m,
                0m,
                isProrata,
                true,
                normalized.WasExcelTimeValue);
        }

        if (!ratePolicy.TryGetMultiplier(category, out decimal multiplier))
        {
            issues.Add(new PayrollCalculationIssue(
                CalculationIssueSeverity.Error,
                "UnconfiguredCategoryRate",
                $"No multiplier is configured for the {category} category.",
                importedRow.SheetName,
                importedRow.SourceRowNumber,
                category));

            return new CalculatedCategory(
                category,
                sourceValue,
                normalized.PayrollHours,
                null,
                isProrata,
                false,
                normalized.WasExcelTimeValue);
        }

        decimal effectiveRate = ratePolicy.HourlyRate * multiplier;
        decimal payableAmount = PayrollRules.CalculatePayableAmount(
            normalized.PayrollHours,
            effectiveRate);

        return new CalculatedCategory(
            category,
            sourceValue,
            normalized.PayrollHours,
            payableAmount,
            isProrata,
            true,
            normalized.WasExcelTimeValue);
    }

    private static IReadOnlyList<EmployeePayrollTotal> BuildEmployeeTotals(
        IEnumerable<CalculatedPayrollRow> rows,
        ICollection<PayrollCalculationIssue> issues)
    {
        var totals = new List<EmployeePayrollTotal>();

        foreach (IGrouping<string, CalculatedPayrollRow> group in rows.GroupBy(row => row.EmployeeNumber))
        {
            if (group.Any(row => !row.TotalPayrollHours.HasValue || !row.TotalPayableAmount.HasValue))
            {
                issues.Add(new PayrollCalculationIssue(
                    CalculationIssueSeverity.Warning,
                    "EmployeeTotalIncomplete",
                    "The employee total excludes rows that require review before calculation can be completed.",
                    group.First().SheetName,
                    group.First().SourceRowNumber));
            }

            var completeRows = group
                .Where(row => row.TotalPayrollHours.HasValue && row.TotalPayableAmount.HasValue)
                .ToList();

            totals.Add(new EmployeePayrollTotal(
                group.Key,
                group.First().EmployeeName,
                completeRows.Sum(row => row.TotalPayrollHours!.Value),
                PayrollRules.RoundCurrency(completeRows.Sum(row => row.TotalPayableAmount!.Value)),
                completeRows.Count,
                group.Count(row => row.IsProrata)));
        }

        return totals;
    }
}

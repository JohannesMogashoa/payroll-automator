using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public sealed class PayrollReviewService
{
    public PayrollReviewSummary Summarize(PayrollCalculationResult calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        var exceptions = new List<PayrollReviewException>();
        exceptions.AddRange(calculation.ImportIssues.Select(MapImportIssue));
        exceptions.AddRange(calculation.Issues.Select(MapCalculationIssue));

        int completeRowCount = calculation.Rows.Count(row =>
            row.TotalPayrollHours.HasValue && row.TotalPayableAmount.HasValue);

        return new PayrollReviewSummary(
            exceptions,
            calculation.Rows.Count,
            completeRowCount,
            calculation.Rows.Count - completeRowCount,
            calculation.Rows.Count(row => row.IsProrata),
            calculation.EmployeeTotals.Sum(total => total.PayrollHours),
            calculation.EmployeeTotals.Sum(total => total.PayableAmount));
    }

    public ReviewConfirmation Confirm(
        PayrollReviewSummary review,
        string reviewer,
        string? comment = null,
        DateTimeOffset? confirmedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (string.IsNullOrWhiteSpace(reviewer))
        {
            throw new ArgumentException("A reviewer is required before export can be confirmed.", nameof(reviewer));
        }

        if (review.HasErrors)
        {
            throw new InvalidOperationException(
                "The review contains errors and cannot be confirmed for export.");
        }

        return new ReviewConfirmation(
            reviewer.Trim(),
            confirmedAtUtc ?? DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            review.WarningCount);
    }

    private static PayrollReviewException MapImportIssue(WorkbookImportIssue issue) =>
        new(
            issue.Severity switch
            {
                ImportIssueSeverity.Warning => PayrollReviewSeverity.Warning,
                ImportIssueSeverity.Error => PayrollReviewSeverity.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(issue))
            },
            issue.Code,
            issue.Message,
            issue.SheetName,
            issue.SourceRowNumber,
            issue.CellAddress,
            null);

    private static PayrollReviewException MapCalculationIssue(PayrollCalculationIssue issue) =>
        new(
            issue.Severity switch
            {
                CalculationIssueSeverity.Warning => PayrollReviewSeverity.Warning,
                CalculationIssueSeverity.Error => PayrollReviewSeverity.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(issue))
            },
            issue.Code,
            issue.Message,
            issue.SheetName,
            issue.SourceRowNumber,
            null,
            issue.Category);
}

using PayrollAutomator.Application;
using PayrollAutomator.Domain;
using Xunit;

namespace PayrollAutomator.Tests.Application;

public sealed class PayrollReviewServiceTests
{
    [Fact]
    public void Summarize_CombinesImportAndCalculationIssuesWithLocations()
    {
        var calculation = new PayrollCalculationResult(
            [],
            [],
            [
                new WorkbookImportIssue(
                    ImportIssueSeverity.Warning,
                    "NegativePayrollValue",
                    "A negative source value requires review.",
                    "Weekly",
                    7,
                    "D7")
            ],
            [
                new PayrollCalculationIssue(
                    CalculationIssueSeverity.Error,
                    "UnconfiguredCategoryRate",
                    "No multiplier is configured.",
                    "Weekly",
                    7,
                    PayrollCategory.Overtime)
            ],
            new PayrollCalculationMetadata(
                PayrollRules.RuleSetVersion,
                PayrollRules.DefaultHourlyRate,
                new Dictionary<PayrollCategory, decimal>
                {
                    [PayrollCategory.Normal] = 1m
                }));

        PayrollReviewSummary summary = new PayrollReviewService().Summarize(calculation);

        Assert.Equal(1, summary.WarningCount);
        Assert.Equal(1, summary.ErrorCount);
        Assert.True(summary.HasErrors);
        Assert.Contains(summary.Exceptions, exception =>
            exception.Code == "NegativePayrollValue" &&
            exception.SourceRowNumber == 7 &&
            exception.CellAddress == "D7");
        Assert.Contains(summary.Exceptions, exception =>
            exception.Code == "UnconfiguredCategoryRate" &&
            exception.Category == PayrollCategory.Overtime);
    }

    [Fact]
    public void Confirm_RejectsReviewsContainingErrors()
    {
        var summary = new PayrollReviewSummary(
            [
                new PayrollReviewException(
                    PayrollReviewSeverity.Error,
                    "InvalidPayrollTimeValue",
                    "Invalid value.",
                    "Weekly",
                    2,
                    "D2",
                    null)
            ],
            1,
            0,
            1,
            0,
            0m,
            0m);

        Assert.Throws<InvalidOperationException>(() =>
            new PayrollReviewService().Confirm(
                summary,
                "Fictional Reviewer",
                confirmedAtUtc: new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Confirm_AllowsWarningsAndRecordsTheirAcknowledgement()
    {
        var summary = new PayrollReviewSummary(
            [
                new PayrollReviewException(
                    PayrollReviewSeverity.Warning,
                    "ProrataRequiresRoster",
                    "Roster hours are required.",
                    "Weekly",
                    2,
                    null,
                    null)
            ],
            1,
            1,
            0,
            1,
            8m,
            1560m);

        ReviewConfirmation confirmation = new PayrollReviewService().Confirm(
            summary,
            "Fictional Reviewer",
            "Reviewed the roster exception.",
            new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal("Fictional Reviewer", confirmation.Reviewer);
        Assert.Equal(1, confirmation.AcknowledgedWarningCount);
        Assert.Equal("Reviewed the roster exception.", confirmation.Comment);
    }
}

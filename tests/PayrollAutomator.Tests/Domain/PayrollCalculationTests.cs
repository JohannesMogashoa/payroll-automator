using PayrollAutomator.Domain;
using Xunit;

namespace PayrollAutomator.Tests.Domain;

public sealed class PayrollCalculationTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.0166666667, 0.0)]
    [InlineData(0.0173611111, 0.5)]
    [InlineData(0.03125, 0.5)]
    [InlineData(0.0319444444, 1.0)]
    [InlineData(0.0416666667, 1.0)]
    public void Normalize_ConvertsExcelTimeValuesUsingMinuteBuckets(
        double sourceValue,
        double expectedHours)
    {
        NormalizedPayrollValue result = PayrollValueNormalizer.Normalize(
            (decimal)sourceValue,
            "h:mm");

        Assert.Equal((decimal)expectedHours, result.PayrollHours);
        Assert.True(result.WasExcelTimeValue);
    }

    [Fact]
    public void Normalize_ConvertsUnformattedDecimalHoursUsingMinuteBuckets()
    {
        NormalizedPayrollValue result = PayrollValueNormalizer.Normalize(8.75m, "General");

        Assert.Equal(8.5m, result.PayrollHours);
        Assert.False(result.WasExcelTimeValue);
    }

    [Fact]
    public void Normalize_RejectsValuesThatDoNotRepresentAWholeMinute()
    {
        FormatException exception = Assert.Throws<FormatException>(
            () => PayrollValueNormalizer.Normalize(0.001m, "General"));

        Assert.Contains("whole minute", exception.Message);
    }

    [Fact]
    public void RatePolicy_RequiresPositiveConfiguredMultipliers()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new PayrollRatePolicy(
                195m,
                new Dictionary<PayrollCategory, decimal>
                {
                    [PayrollCategory.Normal] = 0m
                }));

        Assert.Contains("greater than zero", exception.Message);
    }
}

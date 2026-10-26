using PayrollAutomator.Domain;
using Xunit;

namespace PayrollAutomator.Tests.Domain;

public sealed class PayrollRulesTests
{
    public static TheoryData<int, decimal> ValidMinuteConversions => new()
    {
        { 0, 0m },
        { 1, 0m },
        { 24, 0m },
        { 25, 0.5m },
        { 35, 0.5m },
        { 45, 0.5m },
        { 46, 1m },
        { 59, 1m }
    };

    [Theory]
    [MemberData(nameof(ValidMinuteConversions))]
    public void ConvertMinutesToPayrollHours_MapsMinutesToAgreedBuckets(
        int minutes,
        decimal expectedPayrollHours)
    {
        decimal payrollHours = PayrollRules.ConvertMinutesToPayrollHours(minutes);

        Assert.Equal(expectedPayrollHours, payrollHours);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60)]
    [InlineData(61)]
    public void ConvertMinutesToPayrollHours_RejectsMinutesOutsideAnHour(int minutes)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollRules.ConvertMinutesToPayrollHours(minutes));

        Assert.Equal("minutes", exception.ParamName);
    }

    [Fact]
    public void CalculatePayableAmount_UsesDefaultHourlyRate()
    {
        decimal amount = PayrollRules.CalculatePayableAmount(1m);

        Assert.Equal(195m, amount);
    }

    [Fact]
    public void CalculatePayableAmount_CalculatesHalfHourAtDefaultRate()
    {
        decimal amount = PayrollRules.CalculatePayableAmount(0.5m);

        Assert.Equal(97.50m, amount);
    }

    [Fact]
    public void CalculatePayableAmount_AcceptsCustomHourlyRate()
    {
        decimal amount = PayrollRules.CalculatePayableAmount(2.5m, 100m);

        Assert.Equal(250m, amount);
    }

    [Fact]
    public void CalculatePayableAmount_AllowsZeroHours()
    {
        decimal amount = PayrollRules.CalculatePayableAmount(0m);

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void CalculatePayableAmount_RoundsFinalAmountToCentsAwayFromZero()
    {
        decimal amount = PayrollRules.CalculatePayableAmount(0.005m);

        Assert.Equal(0.98m, amount);
    }

    [Fact]
    public void CalculatePayableAmount_RejectsNegativeHours()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollRules.CalculatePayableAmount(-0.5m));

        Assert.Equal("payrollHours", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculatePayableAmount_RejectsNonPositiveHourlyRate(int hourlyRate)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollRules.CalculatePayableAmount(1m, hourlyRate));

        Assert.Equal("hourlyRate", exception.ParamName);
    }
}

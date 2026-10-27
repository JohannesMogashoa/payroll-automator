namespace PayrollAutomator.Domain;

public static class PayrollRules
{
    public const decimal DefaultHourlyRate = 195m;
    public const string RuleSetVersion = "payroll-rules-v1";

    public static decimal ConvertMinutesToPayrollHours(int minutes)
    {
        if (minutes is < 0 or >= 60)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be between 0 and 59.");
        }

        return minutes switch
        {
            <= 24 => 0m,
            >= 25 and <= 45 => 0.5m,
            _ => 1m
        };
    }

    public static decimal CalculatePayableAmount(
        decimal payrollHours,
        decimal hourlyRate = DefaultHourlyRate)
    {
        if (payrollHours < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payrollHours),
                "Payroll hours cannot be negative.");
        }

        if (hourlyRate <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hourlyRate),
                "Hourly rate must be greater than zero.");
        }

        return RoundCurrency(payrollHours * hourlyRate);
    }

    public static decimal RoundCurrency(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

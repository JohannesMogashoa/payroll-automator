namespace PayrollAutomator.Domain;

public static class PayrollRules
{
    public const decimal DefaultHourlyRate = 195m;

    public static decimal ConvertMinutesToPayrollHours(int minutes)
    {
        if (minutes is < 0 or >= 60)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be between 0 and 59.");
        }

        return minutes switch
        {
            0 => 0m,
            >= 25 and <= 45 => 0.5m,
            _ => Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero)
        };
    }
}

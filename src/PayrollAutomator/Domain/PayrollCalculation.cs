namespace PayrollAutomator.Domain;

public enum PayrollCategory
{
    Normal,
    Overtime,
    Sunday,
    PublicHoliday,
    NightShift,
    SickLeave
}

public sealed class PayrollRatePolicy
{
    private readonly IReadOnlyDictionary<PayrollCategory, decimal> multipliers;

    public PayrollRatePolicy(
        decimal hourlyRate,
        IReadOnlyDictionary<PayrollCategory, decimal> multipliers)
    {
        if (hourlyRate <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hourlyRate),
                "Hourly rate must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(multipliers);

        if (multipliers.Any(pair => pair.Value <= 0m))
        {
            throw new ArgumentException(
                "Category multipliers must be greater than zero.",
                nameof(multipliers));
        }

        this.multipliers = new Dictionary<PayrollCategory, decimal>(multipliers);
        HourlyRate = hourlyRate;
    }

    public decimal HourlyRate { get; }

    public static PayrollRatePolicy Default(decimal hourlyRate = PayrollRules.DefaultHourlyRate) =>
        new(hourlyRate, new Dictionary<PayrollCategory, decimal>
        {
            [PayrollCategory.Normal] = 1m
        });

    public bool TryGetMultiplier(PayrollCategory category, out decimal multiplier) =>
        multipliers.TryGetValue(category, out multiplier);
}

public sealed record NormalizedPayrollValue(
    decimal SourceValue,
    decimal PayrollHours,
    bool WasExcelTimeValue);

public static class PayrollValueNormalizer
{
    public static NormalizedPayrollValue Normalize(
        decimal sourceValue,
        string? numberFormat)
    {
        if (sourceValue < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceValue),
                "Payroll source values cannot be negative when calculating hours.");
        }

        bool wasExcelTimeValue = IsExcelTimeFormat(numberFormat);
        decimal totalMinutes = wasExcelTimeValue
            ? sourceValue * 1440m
            : sourceValue * 60m;

        decimal nearestMinute = Math.Round(
            totalMinutes,
            0,
            MidpointRounding.AwayFromZero);

        // Excel serial values commonly contain a small binary-conversion remainder.
        // Anything larger than that is ambiguous and must be reviewed explicitly.
        if (Math.Abs(totalMinutes - nearestMinute) > 0.0001m)
        {
            throw new FormatException(
                "Payroll source value does not represent a whole minute.");
        }

        long totalMinutesAsLong;
        try
        {
            totalMinutesAsLong = checked((long)nearestMinute);
        }
        catch (OverflowException exception)
        {
            throw new FormatException("Payroll source value is too large.", exception);
        }

        int minutes = (int)(totalMinutesAsLong % 60);
        int wholeHours = checked((int)(totalMinutesAsLong / 60));

        decimal payrollHours = wholeHours + PayrollRules.ConvertMinutesToPayrollHours(minutes);
        return new NormalizedPayrollValue(sourceValue, payrollHours, wasExcelTimeValue);
    }

    private static bool IsExcelTimeFormat(string? numberFormat) =>
        !string.IsNullOrWhiteSpace(numberFormat) &&
        numberFormat.IndexOf('h', StringComparison.OrdinalIgnoreCase) >= 0 &&
        numberFormat.IndexOf('m', StringComparison.OrdinalIgnoreCase) >= 0;
}

public sealed record CalculatedCategory(
    PayrollCategory Category,
    decimal? SourceValue,
    decimal? PayrollHours,
    decimal? PayableAmount,
    bool IsProrata,
    bool IsConfigured,
    bool IsExcelTimeValue);

public sealed record CalculatedPayrollRow(
    string SheetName,
    int SourceRowNumber,
    string PeriodLabel,
    string EmployeeNumber,
    string EmployeeName,
    IReadOnlyList<CalculatedCategory> Categories,
    decimal? TotalPayrollHours,
    decimal? TotalPayableAmount,
    bool IsProrata);

public sealed record EmployeePayrollTotal(
    string EmployeeNumber,
    string EmployeeName,
    decimal PayrollHours,
    decimal PayableAmount,
    int IncludedRowCount,
    int ProrataRowCount);

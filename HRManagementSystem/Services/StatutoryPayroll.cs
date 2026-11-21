namespace HR_KD.Services;

public static class StatutoryPayroll
{
    private static readonly (decimal Month, decimal Hour)[] Wages2026 =
        [(5310000, 25500), (4730000, 22700), (4140000, 20000), (3700000, 17800)];

    private static readonly (decimal Month, decimal Hour)[] Wages2027 =
        [(5700000, 27400), (5080000, 24400), (4450000, 21400), (4040000, 19400)];

    public static (decimal Month, decimal Hour)? MinimumWage(DateOnly date, int region) =>
        date.Year switch
        {
            2026 when region is >= 1 and <= 4 => Wages2026[region - 1],
            2027 when region is >= 1 and <= 4 => Wages2027[region - 1],
            _ => null
        };

    public static decimal ResidentTax2026(decimal monthlyTaxableIncome)
    {
        var income = Math.Max(0, monthlyTaxableIncome);
        return Math.Min(income, 10000000) * .05m
            + Math.Clamp(income - 10000000, 0, 20000000) * .10m
            + Math.Clamp(income - 30000000, 0, 30000000) * .20m
            + Math.Clamp(income - 60000000, 0, 40000000) * .30m
            + Math.Max(0, income - 100000000) * .35m;
    }
}

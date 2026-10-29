namespace FixOrderChallenge.Common;

/// <summary>Instrumentos aceitos pelas duas pontas.</summary>
public static class Symbols
{
    public static readonly IReadOnlyList<string> All = ["PETR4", "VALE3", "VIIA4"];

    public static bool IsValid(string? symbol) => symbol is not null && All.Contains(symbol);
}

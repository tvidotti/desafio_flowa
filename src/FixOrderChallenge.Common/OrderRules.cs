namespace FixOrderChallenge.Common;

/// <summary>
/// Regras de validação de uma nova ordem. Usadas no formulário (OrderGenerator)
/// e novamente no OrderAccumulator, que não deve confiar no que chega pela sessão FIX.
/// </summary>
public static class OrderRules
{
    public const decimal MaxQuantityExclusive = 100_000m;
    public const decimal MaxPriceExclusive = 1_000m;
    public const decimal PriceTick = 0.01m;

    public static Dictionary<string, string[]> Validate(string? symbol, OrderSide? side, decimal? quantity, decimal? price)
    {
        var errors = new Dictionary<string, string[]>();

        if (!Symbols.IsValid(symbol))
            errors["symbol"] = [$"Símbolo deve ser um de: {string.Join(", ", Symbols.All)}."];

        if (side is null || !Enum.IsDefined(side.Value))
            errors["side"] = ["Lado deve ser Compra ou Venda."];

        if (quantity is not { } q || q <= 0 || q >= MaxQuantityExclusive || q % 1 != 0)
            errors["quantity"] = ["Quantidade deve ser um inteiro positivo menor que 100.000."];

        if (price is not { } p || p <= 0 || p >= MaxPriceExclusive || p % PriceTick != 0)
            errors["price"] = ["Preço deve ser positivo, múltiplo de 0,01 e menor que 1.000."];

        return errors;
    }
}

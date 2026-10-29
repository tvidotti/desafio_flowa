using FixOrderChallenge.Common;

namespace OrderGenerator.Orders;

/// <summary>Campos do formulário. Nullable para que a validação devolva mensagens por campo.</summary>
public sealed record OrderRequest(string? Symbol, OrderSide? Side, decimal? Quantity, decimal? Price)
{
    public Dictionary<string, string[]> Validate() => OrderRules.Validate(Symbol, Side, Quantity, Price);
}

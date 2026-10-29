using FixOrderChallenge.Common;

namespace OrderAccumulator.Exposure;

public sealed record ExposureDecision(bool Accepted, decimal CurrentExposure, decimal ProposedExposure);

/// <summary>
/// Mantém a exposição financeira por símbolo:
/// soma(preço × quantidade) das compras − soma(preço × quantidade) das vendas.
/// Uma ordem só é contabilizada se a exposição resultante, em valor absoluto,
/// não ultrapassar o limite. Thread-safe: o QuickFIX/n entrega mensagens em threads
/// de sessão e a checagem + atualização precisam ser atômicas.
/// </summary>
public sealed class ExposureBook
{
    public const decimal DefaultLimit = 100_000_000m;

    private readonly Dictionary<string, decimal> _exposureBySymbol = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public ExposureBook(decimal limit = DefaultLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        Limit = limit;
    }

    public decimal Limit { get; }

    public ExposureDecision TryApply(string symbol, OrderSide side, decimal price, decimal quantity)
    {
        var notional = price * quantity;
        var delta = side == OrderSide.Buy ? notional : -notional;

        lock (_lock)
        {
            var current = _exposureBySymbol.GetValueOrDefault(symbol);
            var proposed = current + delta;

            if (Math.Abs(proposed) > Limit)
                return new ExposureDecision(false, current, proposed);

            _exposureBySymbol[symbol] = proposed;
            return new ExposureDecision(true, proposed, proposed);
        }
    }

    public decimal GetExposure(string symbol)
    {
        lock (_lock)
            return _exposureBySymbol.GetValueOrDefault(symbol);
    }
}

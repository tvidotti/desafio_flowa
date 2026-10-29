using FixOrderChallenge.Common;

namespace OrderGenerator.Orders;

/// <summary>Resumo do ExecutionReport recebido do OrderAccumulator.</summary>
public sealed record OrderResult(
    string ClOrdId,
    string OrderId,
    string ExecType,
    string Symbol,
    OrderSide Side,
    decimal Quantity,
    decimal? Price,
    string? Text);

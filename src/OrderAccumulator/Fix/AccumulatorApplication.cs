using System.Globalization;
using FixOrderChallenge.Common;
using OrderAccumulator.Exposure;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace OrderAccumulator.Fix;

/// <summary>
/// Lado acceptor da sessão FIX. Recebe NewOrderSingle, aplica as regras de exposição
/// e responde com ExecutionReport (ExecType = New ou Rejected).
/// </summary>
public sealed class AccumulatorApplication(ExposureBook exposureBook, ILogger<AccumulatorApplication> logger)
    : MessageCracker, IApplication
{
    private long _orderSequence;

    public void FromApp(Message message, SessionID sessionId) => Crack(message, sessionId);

    public void OnMessage(NewOrderSingle order, SessionID sessionId)
    {
        var clOrdId = order.ClOrdID.Value;
        var symbol = order.Symbol.Value;
        var fixSide = order.Side.Value;
        var quantity = order.OrderQty.Value;
        decimal? price = order.IsSetPrice() ? order.Price.Value : null;
        OrderSide? side = fixSide switch
        {
            Side.BUY => OrderSide.Buy,
            Side.SELL => OrderSide.Sell,
            _ => null,
        };

        var errors = OrderRules.Validate(symbol, side, quantity, price);
        ExecutionReport report;

        if (errors.Count > 0)
        {
            var reason = errors.ContainsKey("symbol") ? OrdRejReason.UNKNOWN_SYMBOL : OrdRejReason.OTHER;
            var text = string.Join(" ", errors.Values.SelectMany(e => e));
            report = Reject(order, reason, text);
            logger.LogWarning("Ordem {ClOrdId} rejeitada por validação: {Text}", clOrdId, text);
        }
        else
        {
            var decision = exposureBook.TryApply(symbol, side!.Value, price!.Value, quantity);

            if (decision.Accepted)
            {
                report = Accept(order, $"Exposição {symbol}: {Format(decision.CurrentExposure)}");
                logger.LogInformation("Ordem {ClOrdId} aceita. Exposição {Symbol} = {Exposure}",
                    clOrdId, symbol, decision.CurrentExposure);
            }
            else
            {
                var text = $"Limite de exposição excedido para {symbol}: atual {Format(decision.CurrentExposure)}, " +
                           $"resultante {Format(decision.ProposedExposure)}, limite {Format(exposureBook.Limit)}";
                report = Reject(order, OrdRejReason.ORDER_EXCEEDS_LIMIT, text);
                logger.LogWarning("Ordem {ClOrdId} rejeitada: {Text}", clOrdId, text);
            }
        }

        Session.SendToTarget(report, sessionId);
    }

    private ExecutionReport Accept(NewOrderSingle order, string text)
    {
        var report = CreateReport(order, ExecType.NEW, OrdStatus.NEW, leavesQty: order.OrderQty.Value);
        report.Set(new Text(text));
        return report;
    }

    private ExecutionReport Reject(NewOrderSingle order, int reason, string text)
    {
        var report = CreateReport(order, ExecType.REJECTED, OrdStatus.REJECTED, leavesQty: 0);
        report.Set(new OrdRejReason(reason));
        report.Set(new Text(text));
        return report;
    }

    private ExecutionReport CreateReport(NewOrderSingle order, char execType, char ordStatus, decimal leavesQty)
    {
        var orderId = Interlocked.Increment(ref _orderSequence).ToString(CultureInfo.InvariantCulture);

        var report = new ExecutionReport(
            new OrderID(orderId),
            new ExecID(Guid.NewGuid().ToString("N")),
            new ExecType(execType),
            new OrdStatus(ordStatus),
            new Symbol(order.Symbol.Value),
            new Side(order.Side.Value),
            new LeavesQty(leavesQty),
            new CumQty(0),
            new AvgPx(0));

        report.Set(new ClOrdID(order.ClOrdID.Value));
        report.Set(new OrderQty(order.OrderQty.Value));
        if (order.IsSetPrice())
            report.Set(new Price(order.Price.Value));

        return report;
    }

    private static string Format(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));

    public void OnCreate(SessionID sessionId) { }
    public void OnLogon(SessionID sessionId) => logger.LogInformation("Logon: {Session}", sessionId);
    public void OnLogout(SessionID sessionId) => logger.LogInformation("Logout: {Session}", sessionId);
    public void ToAdmin(Message message, SessionID sessionId) { }
    public void FromAdmin(Message message, SessionID sessionId) { }
    public void ToApp(Message message, SessionID sessionId) { }
}

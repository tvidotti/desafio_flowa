using System.Collections.Concurrent;
using FixOrderChallenge.Common;
using Microsoft.Extensions.Options;
using OrderGenerator.Orders;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using Message = QuickFix.Message;

namespace OrderGenerator.Fix;

/// <summary>
/// Lado initiator da sessão FIX. Envia NewOrderSingle e correlaciona o ExecutionReport
/// de volta pelo ClOrdID, expondo o fluxo assíncrono do FIX como request/response para a API.
/// </summary>
public sealed class FixOrderClient(IOptions<FixSessionOptions> options, ILogger<FixOrderClient> logger)
    : MessageCracker, IApplication, IHostedService, IDisposable
{
    public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<OrderResult>> _pending = new();
    private SocketInitiator? _initiator;
    private volatile SessionID? _sessionId;

    public bool IsLoggedOn => _sessionId is not null;

    public async Task<OrderResult> SendAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        var sessionId = _sessionId ?? throw new FixSessionUnavailableException();
        var clOrdId = Guid.NewGuid().ToString("N");

        var order = new NewOrderSingle(
            new ClOrdID(clOrdId),
            new Symbol(request.Symbol!),
            new Side(request.Side == OrderSide.Buy ? Side.BUY : Side.SELL),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT));
        order.Set(new OrderQty(request.Quantity!.Value));
        order.Set(new Price(request.Price!.Value));

        var completion = new TaskCompletionSource<OrderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[clOrdId] = completion;

        try
        {
            if (!Session.SendToTarget(order, sessionId))
                throw new FixSessionUnavailableException();

            return await completion.Task.WaitAsync(ResponseTimeout, cancellationToken);
        }
        finally
        {
            _pending.TryRemove(clOrdId, out _);
        }
    }

    public void FromApp(Message message, SessionID sessionId) => Crack(message, sessionId);

    public void OnMessage(ExecutionReport report, SessionID sessionId)
    {
        var clOrdId = report.ClOrdID.Value;
        var execType = report.ExecType.Value switch
        {
            ExecType.NEW => "New",
            ExecType.REJECTED => "Rejected",
            var other => other.ToString(),
        };

        var result = new OrderResult(
            clOrdId,
            report.OrderID.Value,
            execType,
            report.Symbol.Value,
            report.Side.Value == Side.BUY ? OrderSide.Buy : OrderSide.Sell,
            report.OrderQty.Value,
            report.IsSetPrice() ? report.Price.Value : null,
            report.IsSetText() ? report.Text.Value : null);

        logger.LogInformation("ExecutionReport {ClOrdId}: {ExecType} {Text}", clOrdId, execType, result.Text);

        if (_pending.TryGetValue(clOrdId, out var completion))
            completion.TrySetResult(result);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = FixSettingsFactory.CreateInitiator(options.Value);
        _initiator = new SocketInitiator(this, new MemoryStoreFactory(), settings, new FileLogFactory(settings));
        _initiator.Start();
        logger.LogInformation("Conectando ao OrderAccumulator em {Host}:{Port}", options.Value.Host, options.Value.Port);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _initiator?.Stop();
        return Task.CompletedTask;
    }

    public void Dispose() => _initiator?.Dispose();

    public void OnLogon(SessionID sessionId)
    {
        _sessionId = sessionId;
        logger.LogInformation("Logon: {Session}", sessionId);
    }

    public void OnLogout(SessionID sessionId)
    {
        _sessionId = null;
        logger.LogInformation("Logout: {Session}", sessionId);

        foreach (var completion in _pending.Values)
            completion.TrySetException(new FixSessionUnavailableException());
    }

    public void OnCreate(SessionID sessionId) { }
    public void ToAdmin(Message message, SessionID sessionId) { }
    public void FromAdmin(Message message, SessionID sessionId) { }
    public void ToApp(Message message, SessionID sessionId) { }
}

public sealed class FixSessionUnavailableException()
    : Exception("Sessão FIX com o OrderAccumulator não está conectada.");

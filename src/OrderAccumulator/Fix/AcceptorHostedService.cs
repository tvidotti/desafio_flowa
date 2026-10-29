using FixOrderChallenge.Common;
using Microsoft.Extensions.Options;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;

namespace OrderAccumulator.Fix;

public sealed class AcceptorHostedService(
    AccumulatorApplication application,
    IOptions<FixSessionOptions> options,
    ILogger<AcceptorHostedService> logger) : IHostedService, IDisposable
{
    private ThreadedSocketAcceptor? _acceptor;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = FixSettingsFactory.CreateAcceptor(options.Value);
        _acceptor = new ThreadedSocketAcceptor(application, new MemoryStoreFactory(), settings, new FileLogFactory(settings));
        _acceptor.Start();
        logger.LogInformation("OrderAccumulator escutando FIX 4.4 na porta {Port}", options.Value.Port);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _acceptor?.Stop();
        return Task.CompletedTask;
    }

    public void Dispose() => _acceptor?.Dispose();
}

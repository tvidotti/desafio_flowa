using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using FixOrderChallenge.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderAccumulator.Exposure;
using OrderAccumulator.Fix;
using OrderGenerator.Orders;

namespace FixOrderChallenge.Tests;

/// <summary>
/// Sobe o OrderAccumulator (acceptor FIX) e o OrderGenerator (API + initiator FIX) em processo,
/// numa porta livre, e exercita o fluxo completo HTTP → FIX → ExecutionReport → HTTP.
/// </summary>
public sealed class EndToEndTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly int _port = GetFreePort();
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), "fix-tests", Guid.NewGuid().ToString("N"));
    private AcceptorHostedService _accumulator = null!;
    private WebApplicationFactory<Program> _generator = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var accumulatorOptions = Options.Create(new FixSessionOptions
        {
            SenderCompId = "ACCUMULATOR",
            TargetCompId = "GENERATOR",
            Port = _port,
            LogPath = _logPath,
        });
        var application = new AccumulatorApplication(new ExposureBook(), NullLogger<AccumulatorApplication>.Instance);
        _accumulator = new AcceptorHostedService(application, accumulatorOptions, NullLogger<AcceptorHostedService>.Instance);
        await _accumulator.StartAsync(CancellationToken.None);

        _generator = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Fix:Port", _port.ToString())
            .UseSetting("Fix:LogPath", _logPath));
        _client = _generator.CreateClient();

        await WaitForFixLogonAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _generator.DisposeAsync();
        await _accumulator.StopAsync(CancellationToken.None);
        _accumulator.Dispose();
    }

    [Fact]
    public async Task Order_within_limit_returns_execution_report_new()
    {
        var result = await SendAsync("PETR4", OrderSide.Buy, 1_000, 25.50m);

        Assert.Equal("New", result.ExecType);
        Assert.Equal("PETR4", result.Symbol);
        Assert.Equal(OrderSide.Buy, result.Side);
        Assert.Equal(1_000m, result.Quantity);
        Assert.Equal(25.50m, result.Price);
        Assert.False(string.IsNullOrEmpty(result.ClOrdId));
    }

    [Fact]
    public async Task Order_exceeding_limit_returns_rejected_and_does_not_count()
    {
        Assert.Equal("New", (await SendAsync("VALE3", OrderSide.Buy, 99_999, 999.99m)).ExecType);

        var rejected = await SendAsync("VALE3", OrderSide.Buy, 99_999, 999.99m);
        Assert.Equal("Rejected", rejected.ExecType);
        Assert.Contains("Limite de exposição excedido", rejected.Text);

        // Como a ordem rejeitada não entrou na conta, uma venda leva a exposição de volta a zero.
        Assert.Equal("New", (await SendAsync("VALE3", OrderSide.Sell, 99_999, 999.99m)).ExecType);
        Assert.Equal("New", (await SendAsync("VALE3", OrderSide.Sell, 99_999, 999.99m)).ExecType);
    }

    [Fact]
    public async Task Invalid_form_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/orders",
            new OrderRequest("ITUB4", OrderSide.Buy, 100_000, 10.001m), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("symbol", out _));
        Assert.True(errors.TryGetProperty("quantity", out _));
        Assert.True(errors.TryGetProperty("price", out _));
    }

    private async Task<OrderResult> SendAsync(string symbol, OrderSide side, decimal quantity, decimal price)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new OrderRequest(symbol, side, quantity, price), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResult>(Json))!;
    }

    private async Task WaitForFixLogonAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var status = await _client.GetFromJsonAsync<JsonElement>("/api/status");
            if (status.GetProperty("fixConnected").GetBoolean())
                return;
            await Task.Delay(100);
        }

        throw new TimeoutException("Sessão FIX não fez logon a tempo.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

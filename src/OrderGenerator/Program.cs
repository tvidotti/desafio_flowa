using System.Text.Json.Serialization;
using FixOrderChallenge.Common;
using OrderGenerator.Fix;
using OrderGenerator.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FixSessionOptions>(builder.Configuration.GetSection("Fix"));
builder.Services.AddSingleton<FixOrderClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FixOrderClient>());
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", (FixOrderClient client) =>
    new { fixConnected = client.IsLoggedOn, symbols = Symbols.All });

app.MapPost("/api/orders", async (OrderRequest request, FixOrderClient client, CancellationToken ct) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    try
    {
        return Results.Ok(await client.SendAsync(request, ct));
    }
    catch (FixSessionUnavailableException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException)
    {
        return Results.Problem("O OrderAccumulator não respondeu a tempo.", statusCode: StatusCodes.Status504GatewayTimeout);
    }
});

app.Run();

public partial class Program;

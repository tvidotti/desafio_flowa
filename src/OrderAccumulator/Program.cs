using FixOrderChallenge.Common;
using OrderAccumulator.Exposure;
using OrderAccumulator.Fix;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<FixSessionOptions>(builder.Configuration.GetSection("Fix"));
builder.Services.AddSingleton(new ExposureBook());
builder.Services.AddSingleton<AccumulatorApplication>();
builder.Services.AddHostedService<AcceptorHostedService>();

builder.Build().Run();

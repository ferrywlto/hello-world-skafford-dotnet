using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

Meter myMeter = new("skaffold-dotnet-hello");
Counter<int> requestCounter = myMeter.CreateCounter<int>("hello-request-counter");


// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(builder => builder
        .AddService(serviceName: "OpenTelementry Getting Started"))
        .WithMetrics(builder => builder
            .AddMeter("skaffold-dotnet-hello")
            .AddPrometheusExporter()
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
            {
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            })
        );

var app = builder.Build();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
configurationBuilder.AddEnvironmentVariables();
var config = configurationBuilder.Build();
var worldServiceAddress = config["WORLD_SERVICE_ADDRESS"];

app.MapGet("/hello/{name}", (string name) =>
{
    requestCounter.Add(1);
    return $"Hello {name}!";
})
.WithName("Hello")
.WithOpenApi();

app.MapGet("/hello/world", async () =>
{
    Console.WriteLine($"{worldServiceAddress}");

    var client = new HttpClient();
    var result = await client.GetStringAsync($"{worldServiceAddress}/world");
    return $"Response from World: {result}";
})
.WithName("HelloWorld")
.WithOpenApi();

app.Run();
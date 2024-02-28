using System.Text;

var builder = WebApplication.CreateBuilder(args);

var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var statuses = new[]
{
    "Freezing", "Melting", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/world", (HttpContext ctx) =>
{
    var rng = new Random();
    var localAddress = ctx.Connection?.LocalIpAddress?.ToString();
    var localPort = ctx.Connection?.LocalPort.ToString();
    var remoteAddress = ctx.Connection?.RemoteIpAddress?.ToString();
    var remotePort = ctx.Connection?.RemotePort.ToString();
    var sb = new StringBuilder();
    sb.AppendLine($"The world is {statuses[rng.Next(statuses.Length)]}.");
    sb.AppendLine($"Request Info: Local: {localAddress}:{localPort}, Remote: {remoteAddress}:{remotePort}");
    sb.AppendLine($"Config Info: foo:{config["foo"]}");
    
    return sb.ToString();
})
.WithName("World")
.WithOpenApi();

app.Run();
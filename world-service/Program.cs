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
    
    sb.AppendLine($"Environment: [{app.Environment.EnvironmentName}]");
    sb.AppendLine($"Request Info: Local: {localAddress}:{localPort}, Remote: {remoteAddress}:{remotePort}");

    if(app.Environment.IsProduction())
    {
        sb.AppendLine($"Pod Name: {config["POD_NAME"]}");
        sb.AppendLine($"Pod Address: {config["POD_ADDRESS"]}");
        sb.AppendLine($"Node Name: {config["NODE_NAME"]}");

        sb.AppendLine($"""
        Config Info: 
        From env:
        - foo: {config["foo"]}
        - which_config: {config["which_config"]}
        """);

        // try to read mounted config file
        var configMountPath = "/etc/config";
        if (Directory.Exists(configMountPath))
        {
            sb.AppendLine($"From mounted volume:");
            var files = Directory.GetFiles(configMountPath);
            foreach(var file in files)
            {
                var fileName = Path.GetFileName(file);
                var content = File.ReadAllText(file);

                sb.AppendLine($"""
                - file: {fileName}
                - content: {content}
                """);
            }
        }

        sb.AppendLine("Secrets: ");
        if (!string.IsNullOrEmpty(config["SECRET_DUMMY"]))
        {
            sb.AppendLine($"""
            From env:
            - dummy: {config["SECRET_DUMMY"]}
            """);
        }
        var secretMountPath = "/etc/secret";
        if (Directory.Exists(secretMountPath))
        {
            sb.AppendLine($"From secret volume:");
            var files = Directory.GetFiles(secretMountPath);
            foreach(var file in files)
            {
                var fileName = Path.GetFileName(file);
                var content = File.ReadAllText(file);

                sb.AppendLine($"""
                - file: {fileName}
                - content: {content}
                """);
            }
        }
    }
    return sb.ToString();
})
.WithName("World")
.WithOpenApi();

app.Run();
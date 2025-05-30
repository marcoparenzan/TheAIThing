using Calculator.Tools;
using ModelContextProtocol.Protocol;
using System.Text;

if (args.Length > 0)
{
    if (args[0] == "stdio-bridge")
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        while (true)
        {
            try
            {
                var line = Console.ReadLine();
                if (line is null) break;
                var response = await httpClient.PostAsync(args[1],
                    new StringContent(line, Encoding.UTF8, "application/json"));
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);       
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
	.AddMcpServer()
        .WithTools<McpCalculatorServer>()
        .WithTools<McpResourceListHandler>()  // Add the resource list handler
        .WithToolsFromAssembly()
		.WithHttpTransport();   

// Add CORS services
builder.Services.AddCors();

var app = builder.Build();

// Configure CORS middleware
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapMcp();

app.Run();
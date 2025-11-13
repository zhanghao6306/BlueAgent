using Agent.Abstractions;
using Agent.Core;
using Agent.Tools;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(opt =>
{
    opt.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2);
});

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// DI
builder.Services.AddGrpc();
builder.Services.AddSingleton<IPlanner, SimplePlanner>();
builder.Services.AddSingleton<IAgent, DefaultAgent>();
builder.Services.AddSingleton<ITool, CalculatorTool>();

var app = builder.Build();
app.MapGrpcService<AgentGrpcService>();
app.MapGet("/", () => "BlueAgent gRPC Server is running.");
app.Run();

using Agent.Abstractions;
using Agent.Core;
using Agent.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var services = new ServiceCollection();

// Core
services.AddSingleton<IPlanner, SimplePlanner>();
services.AddSingleton<IAgent, DefaultAgent>();

// Tools — 后续由 Trae 自动追加
services.AddSingleton<ITool, CalculatorTool>();

// 条件注册 SkChatTool
var kernel = KernelFactory.CreateKernel();
if (kernel != null)
{
    services.AddSingleton<IKernel>(kernel);
    services.AddSingleton<ITool, SkChatTool>();
}

var sp = services.BuildServiceProvider();
var agent = sp.GetRequiredService<IAgent>();

string input = args.Length > 0 ? string.Join(' ', args) : "calc add 2 3";
var result = await agent.RunAsync(input);
if (result.Success)
    Log.Information("✅ {Output}", result.Output);
else
    Log.Error("❌ {Output}", result.Output);

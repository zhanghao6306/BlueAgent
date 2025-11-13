using Agent.Abstractions;
using Agent.Core;
using Agent.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agent.Tests;

public class AgentSmokeTests
{
    private IAgent CreateAgent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPlanner, SimplePlanner>();
        services.AddSingleton<IAgent, DefaultAgent>();
        services.AddSingleton<ITool, CalculatorTool>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IAgent>();
    }

    [Fact]
    public async Task Calc_Add_Works()
    {
        var agent = CreateAgent();
        var r = await agent.RunAsync("calc add 2 3");
        Assert.True(r.Success);
        Assert.Equal("5", r.Output);
    }

    [Fact]
    public async Task Unknown_Tool_Fails()
    {
        var agent = CreateAgent();
        var r = await agent.RunAsync("echo hello");
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Calc_Div_By_Zero_Fails()
    {
        var agent = CreateAgent();
        var r = await agent.RunAsync("calc div 1 0");
        Assert.False(r.Success);
    }
}

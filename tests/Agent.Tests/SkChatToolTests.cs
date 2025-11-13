using Agent.Abstractions;
using Agent.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agent.Tests;

public class SkChatToolTests
{
    private IAgent CreateAgentWithSkChatTool()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPlanner, SimplePlanner>();
        services.AddSingleton<IAgent, DefaultAgent>();
        services.AddSingleton<IKernel, FakeKernel>();
        services.AddSingleton<ITool, SkChatTool>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IAgent>();
    }

    [Fact]
    public async Task SkChat_Works()
    {
        var agent = CreateAgentWithSkChatTool();
        var r = await agent.RunAsync("sk.chat 你好");
        Assert.True(r.Success);
        Assert.Contains("你好", r.Output);
    }

    [Fact]
    public async Task SkChat_Without_Prompt_Fails()
    {
        var agent = CreateAgentWithSkChatTool();
        var r = await agent.RunAsync("sk.chat");
        Assert.False(r.Success);
        Assert.Contains("请提供聊天提示词", r.Output);
    }

    [Fact]
    public async Task SkChat_With_Long_Prompt_Works()
    {
        var agent = CreateAgentWithSkChatTool();
        var r = await agent.RunAsync("sk.chat 请解释什么是人工智能");
        Assert.True(r.Success);
        Assert.Contains("什么是人工智能", r.Output);
    }
}
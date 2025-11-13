using Agent.Abstractions;

namespace Agent.Core;

public class DefaultAgent : IAgent
{
    private readonly IServiceProvider _sp;
    private readonly IPlanner _planner;
    private readonly IReadOnlyDictionary<string, ITool> _toolMap;
    private readonly IReadOnlyList<ITool> _tools;

    public DefaultAgent(IServiceProvider sp, IPlanner planner, IEnumerable<ITool> tools)
    {
        _sp = sp;
        _planner = planner;
        _tools = tools.ToList();
        _toolMap = tools.ToDictionary(t => t.Name.ToLowerInvariant(), t => t);
    }

    public async Task<AgentResult> RunAsync(string input, CancellationToken ct = default)
    {
        var route = _planner.Route(input);
        if (route == null)
        {
            return new AgentResult(false, "未识别的指令。示例：`calc add 2 3`");
        }

        if (!_toolMap.TryGetValue(route.Value.tool, out var tool))
        {
            return new AgentResult(false, $"未找到工具：{route.Value.tool}");
        }

        var ctx = new ToolContext { Input = input, Args = route.Value.args };
        try
        {
            return await tool.InvokeAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            return new AgentResult(false, $"工具执行失败：{ex.Message}");
        }
    }

    public IReadOnlyList<ITool> GetTools()
    {
        return _tools;
    }
}

using Agent.Abstractions;

namespace Agent.Tools;

public class CalculatorTool : ITool
{
    public string Name => "calc";
    public string Description => "简单计算器：calc add|sub|mul|div a b";

    public Task<AgentResult> InvokeAsync(ToolContext ctx, CancellationToken ct = default)
    {
        if (ctx.Args.Count < 3)
            return Task.FromResult(new AgentResult(false, "用法：calc add|sub|mul|div a b"));

        var op = ctx.Args[0].ToLowerInvariant();
        if (!double.TryParse(ctx.Args[1], out var a) || !double.TryParse(ctx.Args[2], out var b))
            return Task.FromResult(new AgentResult(false, "参数必须是数字"));

        double result = op switch
        {
            "add" => a + b,
            "sub" => a - b,
            "mul" => a * b,
            "div" => b == 0 ? double.NaN : a / b,
            _ => double.NaN
        };

        if (double.IsNaN(result))
            return Task.FromResult(new AgentResult(false, "未知操作或非法参数"));

        return Task.FromResult(new AgentResult(true, result.ToString()));
    }
}

namespace Agent.Abstractions;

public record AgentResult(bool Success, string Output, IDictionary<string, object>? Data = null);

public class ToolContext
{
    public string Input { get; init; } = string.Empty;
    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();
    public IDictionary<string, object> Memory { get; } = new Dictionary<string, object>();
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<AgentResult> InvokeAsync(ToolContext ctx, CancellationToken ct = default);
}

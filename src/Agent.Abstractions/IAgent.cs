namespace Agent.Abstractions;

public interface IAgent
{
    Task<AgentResult> RunAsync(string input, CancellationToken ct = default);
    IReadOnlyList<ITool> GetTools();
}

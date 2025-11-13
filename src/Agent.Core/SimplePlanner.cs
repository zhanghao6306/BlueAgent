using Agent.Abstractions;

namespace Agent.Core;

public interface IPlanner
{
    (string tool, IReadOnlyList<string> args)? Route(string input);
}

public class SimplePlanner : IPlanner
{
    public (string tool, IReadOnlyList<string> args)? Route(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        // 约定：第一个词为 tool 名称
        var tool = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();
        return (tool, args);
    }
}

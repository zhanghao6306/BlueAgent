using Agent.Abstractions;
using Microsoft.SemanticKernel;

namespace Agent.Core;

public class SkChatTool : ITool
{
    private readonly IKernel _kernel;

    public SkChatTool(IKernel kernel)
    {
        _kernel = kernel;
    }

    public string Name => "sk.chat";

    public string Description => "使用Semantic Kernel进行聊天对话";

    public async Task<AgentResult> InvokeAsync(ToolContext ctx, CancellationToken ct = default)
        {
            if (ctx.Args.Count == 0)
            {
                return new AgentResult(false, "请提供聊天提示词");
            }

            var prompt = string.Join(" ", ctx.Args);
            
            try
            {
                // 使用Kernel进行聊天
                var result = await _kernel.InvokePromptAsync<string>(prompt, cancellationToken: ct);
                return new AgentResult(true, result);
            }
            catch (Exception ex)
        {
            return new AgentResult(false, $"聊天失败：{ex.Message}");
        }
    }
}
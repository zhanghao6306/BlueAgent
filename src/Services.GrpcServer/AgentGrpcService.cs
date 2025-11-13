using Agent.Abstractions;
using Grpc.Core;
using Agent;

public class AgentGrpcService : AgentService.AgentServiceBase
{
    private readonly IAgent _agent;
    public AgentGrpcService(IAgent agent) => _agent = agent;

    public override async Task<RunReply> Run(RunRequest request, ServerCallContext context)
    {
        var r = await _agent.RunAsync(request.Input, context.CancellationToken);
        return new RunReply { Success = r.Success, Output = r.Output };
    }

    public override Task<ToolMetaReply> ToolMeta(ToolMetaRequest request, ServerCallContext context)
    {
        var tools = _agent.GetTools();
        var reply = new ToolMetaReply();
        foreach (var tool in tools)
        {
            reply.Tools.Add(new ToolMeta { Name = tool.Name, Description = tool.Description });
        }
        return Task.FromResult(reply);
    }
}

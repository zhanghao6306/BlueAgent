using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core;

public class FakeKernel : IKernel
{
    public Task<T> InvokePromptAsync<T>(string prompt, CancellationToken cancellationToken = default)
    {
        var result = $"Fake response for: {prompt}";
        return Task.FromResult((T)Convert.ChangeType(result, typeof(T)));
    }
}
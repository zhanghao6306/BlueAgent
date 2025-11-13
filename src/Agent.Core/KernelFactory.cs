using Microsoft.SemanticKernel;

namespace Agent.Core;

public interface IKernel
{
    Task<T> InvokePromptAsync<T>(string prompt, CancellationToken cancellationToken = default);
}

public class KernelWrapper : IKernel
{
    private readonly Kernel _kernel;

    public KernelWrapper(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<T> InvokePromptAsync<T>(string prompt, CancellationToken cancellationToken = default)
    {
        var result = await _kernel.InvokePromptAsync(prompt, arguments: null, cancellationToken: cancellationToken);
        return (T)Convert.ChangeType(result.ToString(), typeof(T));
    }
}

public static class KernelFactory
{
    public static IKernel? CreateKernel()
    {
            try
            {
                // 检查是否有必要的配置
                // 这里可以根据实际情况读取配置文件或环境变量
                // 例如：检查是否有OpenAI API密钥
                var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return null;
                }
                
                // 创建Kernel实例
                var kernel = new Kernel();
                return new KernelWrapper(kernel);
            }
            catch
            {
                // 配置错误时返回null
                return null;
            }
        }
}
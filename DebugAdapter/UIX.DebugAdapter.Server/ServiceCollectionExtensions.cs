using Microsoft.Extensions.DependencyInjection;
using Microsoft.Iris.Debug;

namespace Microsoft.Iris.DebugAdapter.Server;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIrisDebugServices(this IServiceCollection services, IDebuggerServer debuggerServer, string symbolDir, string? sourceDir)
    {
        DebugSymbolResolver symbolResolver = new(symbolDir, sourceDir);

        return services
            .AddSingleton(symbolResolver)
            .AddSingleton(debuggerServer);
    }
}

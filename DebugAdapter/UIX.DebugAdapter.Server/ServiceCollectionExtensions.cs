using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Iris.DebugAdapter.Server;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIrisDebugServices(this IServiceCollection services, string symbolDir, string? sourceDir)
    {
        DebugSymbolResolver symbolResolver = new(symbolDir, sourceDir);

        return services
            .AddSingleton(symbolResolver);
    }
}

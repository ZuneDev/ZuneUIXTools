using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class SourceHandler(DebugSymbolResolver symbolResolver) : ISourceHandler, ILoadedSourcesHandler
{
    private const string MIME_IRIS_UIX_XML = "application/vnd.microsoft.iris.uix+xml";

    public async Task<SourceResponse> Handle(SourceArguments request, CancellationToken cancellationToken)
    {
        Debugger.Launch();
        Debugger.Break();

        var fsym = symbolResolver.GetForFile(request.Source?.Name);
        if (fsym is null)
            return new SourceResponse();

        if (fsym.SourceFileName is null || !File.Exists(fsym.SourceFileName))
            return new();

        string text =
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            await File.ReadAllTextAsync(fsym.SourceFileName, cancellationToken);
#else
            File.ReadAllText(fsym.SourceFileName);
#endif

        return new()
        {
            Content = text,
            MimeType = MIME_IRIS_UIX_XML
        };
    }

    public async Task<LoadedSourcesResponse> Handle(LoadedSourcesArguments request, CancellationToken cancellationToken)
    {
        Debugger.Launch();
        Debugger.Break();

        return new()
        {
            Sources = []
        };
    }
}

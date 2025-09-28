using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System.Threading;
using System.Threading.Tasks;

namespace IrisLanguageServer;

public class TextDocumentHandler(ILogger<TextDocumentHandler> logger, ILanguageServerConfiguration config) : TextDocumentSyncHandlerBase
{
    private readonly TextDocumentSelector _documentSelector = new(
        new TextDocumentFilter
        {
            Pattern = "**/*.uix"
        }
    );

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "uix-xml");

    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken token)
    {
        await Task.Yield();
        logger.LogInformation("Hello world!");
        await config.GetScopedConfiguration(request.TextDocument.Uri, token).ConfigureAwait(false);
        return Unit.Value;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken token)
    {
        logger.LogCritical("Critical");
        logger.LogDebug("Debug");
        logger.LogTrace("Trace");
        logger.LogInformation("Hello world!");

        foreach (var change in request.ContentChanges)
        {
            logger.LogInformation("{range}:\t{text}", change.Range, change.Text);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken token) => Unit.Task;

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken token)
    {
        if (config.TryGetScopedConfiguration(request.TextDocument.Uri, out var disposable))
        {
            disposable.Dispose();
        }

        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new()
        {
            DocumentSelector = _documentSelector,
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = true },
        };
    }
}
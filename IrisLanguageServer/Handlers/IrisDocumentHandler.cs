using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Language.Xml;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IrisLanguageServer.Handlers;

public class IrisDocumentHandler(DocumentContentRepository contentRepo, ILogger<IrisDocumentHandler> logger, ILanguageServerConfiguration config) : TextDocumentSyncHandlerBase
{
    private readonly TextDocumentSelector _documentSelector = new(
        new TextDocumentFilter
        {
            Pattern = "**/*.uix"
        },
        new TextDocumentFilter
        {
            Pattern = "**/*.uix-xml"
        },
        new TextDocumentFilter
        {
            Pattern = "**/*.uixa"
        },
        new TextDocumentFilter
        {
            Pattern = "**/*.uix-script"
        }
    );

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        var fileExtension = System.IO.Path.GetExtension(uri.Path).ToLowerInvariant();
        var languageId = fileExtension switch
        {
            ".uix" => "uix-xml",
            _ => fileExtension[1..]
        };
        return new(uri, languageId);
    }

    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken token)
    {
        await Task.Yield();

        var docConfig = await config.GetScopedConfiguration(request.TextDocument.Uri, token).ConfigureAwait(false);

        contentRepo.AddOrUpdateContent(request.TextDocument);

        var xml = Parser.ParseText(request.TextDocument.Text);

        return Unit.Value;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken token)
    {
        var change = request.ContentChanges.Single();
        contentRepo.AddOrUpdateContent(request.TextDocument.Uri, change.Text);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken token) => Unit.Task;

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken token)
    {
        if (config.TryGetScopedConfiguration(request.TextDocument.Uri, out var disposable))
        {
            disposable.Dispose();
        }

        contentRepo.DiscardDocument(request.TextDocument.Uri.ToString());

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
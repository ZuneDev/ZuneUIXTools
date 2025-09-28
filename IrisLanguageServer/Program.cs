using IrisLanguageServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

await System.IO.File.WriteAllTextAsync("E:\\Repos\\ZuneDev\\ZuneUIXTools\\marker.txt", "hi, I launched");

Debugger.Launch();

var server = await LanguageServer.From(options =>
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput())
        .WithLoggerFactory(new LoggerFactory())
        .AddDefaultLoggingProvider()
        .WithServices(ConfigureServices)
        .WithHandler<TextDocumentHandler>()
        .OnInitialize(OnLanguageServerInitialize)
        .OnInitialized(OnLanguageServerInitialized)
        .OnStarted(OnLanguageServerStarted)
);

await server.WaitForExit;

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<BufferManager>();
}

async Task OnLanguageServerInitialize(ILanguageServer server, InitializeParams request, CancellationToken cancellationToken)
{

}

async Task OnLanguageServerInitialized(ILanguageServer server, InitializeParams request, InitializeResult response, CancellationToken cancellationToken)
{

}

async Task OnLanguageServerStarted(ILanguageServer server, CancellationToken cancellationToken)
{

}
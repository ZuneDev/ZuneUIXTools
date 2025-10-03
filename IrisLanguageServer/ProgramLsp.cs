using IrisLanguageServer.Handlers;
using IrisLanguageServer.Handlers.Dap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Markup;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IrisLanguageServer;

public static class ProgramLsp
{
    public static async Task<ILanguageServer> InitializeAsync(Stream input, Stream output, IDisposable? disposable = null)
    {
        var lspOptions = new LanguageServerOptions()
            .WithLoggerFactory(new LoggerFactory())
            .AddDefaultLoggingProvider()
            .ConfigureLogging(loggingBuilder => loggingBuilder.AddLanguageProtocolLogging().SetMinimumLevel(LogLevel.Trace))
            .WithServices(ConfigureServices)
            .WithHandler<IrisDocumentHandler>()
            .OnInitialize(OnLanguageServerInitialize)
            .OnInitialized(OnLanguageServerInitialized)
            .OnStarted(OnLanguageServerStarted)
            .WithInput(input)
            .WithOutput(output);

        if (disposable is not null)
            lspOptions.RegisterForDisposal(disposable);

        var server = LanguageServer.PreInit(lspOptions);
        await server.Initialize(default);

        return server;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<BufferManager>();
        services.AddSingleton<DocumentContentRepository>();

        // This allows us to log normal messages over std error
        // which then ensures that all communication over std out are LSP messages
        services.Configure<ConsoleLoggerOptions>(z => z.LogToStandardErrorThreshold = LogLevel.Trace);
    }

    private static async Task OnLanguageServerInitialize(ILanguageServer server, InitializeParams request, CancellationToken cancellationToken)
    {
        server.LogInfo("Server initializing...");

        server.LogInfo("Copying Zune support files...");
        string zuneDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zune");
        if (Directory.Exists(zuneDirectory))
        {
            // Zune is installed, attempt to copy the required UIX dependencies
            var workingDirectory = Environment.CurrentDirectory;
            IEnumerable<string> irisDependencies = ["UIXRender.dll", "UIXControls.dll"];

            foreach (string irisDependency in irisDependencies)
            {
                var dstFilePath = Path.Combine(workingDirectory, irisDependency);
                if (Path.Exists(dstFilePath))
                    continue;

                var srcFilePath = Path.Combine(zuneDirectory, irisDependency);
                if (!Path.Exists(srcFilePath))
                    continue;

                File.Copy(srcFilePath, dstFilePath);
            }
        }

        server.LogInfo("Starting markup system...");

        MarkupSystem.Startup(true);
        Assembler.RegisterLoader();
    }

    private static async Task OnLanguageServerInitialized(ILanguageServer server, InitializeParams request, InitializeResult response, CancellationToken cancellationToken)
    {
        server.LogInfo("Server initialized");
    }

    private static async Task OnLanguageServerStarted(ILanguageServer server, CancellationToken cancellationToken)
    {
        server.LogInfo($"Server started");
    }
}

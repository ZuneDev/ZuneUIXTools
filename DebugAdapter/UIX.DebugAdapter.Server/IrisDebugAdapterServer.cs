using Microsoft.Extensions.DependencyInjection;
using Microsoft.Iris.Debug;
using Microsoft.Iris.DebugAdapter.Server.Handlers;
using OmniSharp.Extensions.DebugAdapter.Server;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Iris.DebugAdapter.Server;

public class IrisDebugAdapterServer : IDisposable
{
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly TaskCompletionSource<bool> _serverStopped;

    public IrisDebugAdapterServer(
        Stream inputStream,
        Stream outputStream,
        IrisDebugServerOptions options)
    {
        _inputStream = inputStream;
        _outputStream = outputStream;
        _serverStopped = new();

        Options = options;
    }

    internal IrisDebugServerOptions Options { get; }

    internal DebugAdapterServer? Server { get; private set; }

    /// <summary>
    /// Start the debug server listening.
    /// </summary>
    /// <returns>A task that completes when the server is ready.</returns>
    public async Task StartAsync()
    {
        Server = await DebugAdapterServer.From(options =>
        {
            // We need to let the PowerShell Context Service know that we are in a debug session
            // so that it doesn't send the powerShell/startDebugger message.
            //_psesHost = ServiceProvider.GetService<PsesInternalHost>();
            //_psesHost.DebugContext.IsDebugServerActive = true;

            options
                .WithInput(_inputStream)
                .WithOutput(_outputStream)
                .WithServices(serviceCollection =>
                    serviceCollection
                        .AddOptions()
                        .AddIrisDebugServices(Options.SymbolDir, Options.SourceDir)
                )
                // TODO: Consider replacing all WithHandler with AddSingleton
                //.WithHandler<AttachHandler>()
                //.WithHandler<DisconnectHandler>()
                .WithHandler<BreakpointHandler>()
                //.WithHandler<ConfigurationDoneHandler>()
                //.WithHandler<ThreadsHandler>()
                //.WithHandler<StackTraceHandler>()
                //.WithHandler<ScopesHandler>()
                //.WithHandler<VariablesHandler>()
                .WithHandler<ContinueHandler>()
                .WithHandler<NextHandler>()
                .WithHandler<PauseHandler>()
                .WithHandler<StepInHandler>()
                //.WithHandler<StepOutHandler>()
                //.WithHandler<SourceHandler>()
                //.WithHandler<SetVariableHandler>()
                //.WithHandler<DebugEvaluateHandler>()
                // The OnInitialize delegate gets run when we first receive the _Initialize_ request:
                // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                .OnInitialize(async (server, _, cancellationToken) =>
                {
                    // Start the host if not already started, and enable debug mode (required
                    // for remote debugging).
                    //
                    // TODO: We might need to fill in HostStartOptions here.
                    //_startedPses = !await _psesHost.TryStartAsync(new HostStartOptions(), cancellationToken).ConfigureAwait(false);
                    //_psesHost.DebugContext.EnableDebugMode();

                    // Clear any existing breakpoints before proceeding.
                    //BreakpointService breakpointService = server.GetService<BreakpointService>();
                    //await breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(false);
                })
                // The OnInitialized delegate gets run right before the server responds to the _Initialize_ request:
                // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                .OnInitialized((_, _, response, _) =>
                {
                    //response.SupportsConditionalBreakpoints = true;
                    //response.SupportsConfigurationDoneRequest = true;
                    //response.SupportsFunctionBreakpoints = true;
                    //response.SupportsHitConditionalBreakpoints = true;
                    //response.SupportsLogPoints = true;
                    //response.SupportsSetVariable = true;
                    //response.SupportsDelayedStackTraceLoading = true;

                    return Task.CompletedTask;
                })
            ;
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        // Note that the lifetime of the DebugContext is longer than the debug server;
        // It represents the debugger on the PowerShell process we're in,
        // while a new debug server is spun up for every debugging session
        //_psesHost.DebugContext.IsDebugServerActive = false;
        Server?.Dispose();
        _inputStream.Dispose();
        _outputStream.Dispose();
        _serverStopped.SetResult(true);
    }

    public async Task WaitForShutdownAsync() => await _serverStopped.Task.ConfigureAwait(false);

    public event EventHandler? SessionEnded;

    internal void OnSessionEnded() => SessionEnded?.Invoke(this, EventArgs.Empty);
}

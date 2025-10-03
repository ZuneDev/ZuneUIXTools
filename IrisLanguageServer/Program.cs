using CommandLine;
using IrisLanguageServer;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;

var parser = new Parser(settings => 
{
    settings.IgnoreUnknownArguments = true;
});

var parseResult = parser.ParseArguments<CommandLineOptions>(args);

if (parseResult.Errors.Any())
{
    Environment.Exit(1);
}

var options = parseResult.Value;

if (options.WaitForDebugger)
{
    Debugger.Launch();
}

Stream input, output;
IDisposable? disposable = null;

if (options.Pipe is { } pipeName)
{
    if (pipeName.StartsWith(@"\\.\pipe\"))
    {
        // VSCode on Windows prefixes the pipe with \\.\pipe\
        pipeName = pipeName[@"\\.\pipe\".Length..];
    }

    var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

    await clientPipe.ConnectAsync();

    input = clientPipe;
    output = clientPipe;
    disposable = clientPipe;
}
else if (options.Socket is { } port)
{
    var tcpClient = new TcpClient();

    await tcpClient.ConnectAsync(IPAddress.Loopback, port);
    var tcpStream = tcpClient.GetStream();

    input = tcpStream;
    output = tcpStream;
    disposable = tcpClient;
}
else
{
    input = Console.OpenStandardInput();
    output = Console.OpenStandardOutput();
}

var lsp = await ProgramLsp.InitializeAsync(input, output, disposable).ConfigureAwait(false);

await lsp.WaitForExit;

// Runs after server is exited
lsp.Dispose();

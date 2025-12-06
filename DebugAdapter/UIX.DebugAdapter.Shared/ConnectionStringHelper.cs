using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Microsoft.Iris.DebugAdapter;

public static class ConnectionStringHelper
{
    private static readonly bool _useDuplex = false;

    public static void CreateFromString(string connectionString, out Stream input, out Stream output)
    {
        // TODO: Support other transports
        if (NamedPipeUtils.TryGetPipeName(connectionString, out var pipeName))
        {
            Console.WriteLine($"Using pipe name {pipeName}");
            if (_useDuplex)
            {
                var pipe = NamedPipeUtils.CreateNamedPipe(pipeName, PipeDirection.InOut);

                pipe.WaitForConnection();

                var loggingStream = new LoggingStream(pipe, LoggingStream_OnRead, LoggingStream_OnWrite);
                
                input = loggingStream;
                output = loggingStream;
            }
            else
            {
                var pipeToClientName = pipeName + "_ToClient";
                var pipeToClient = NamedPipeUtils.CreateNamedPipe(pipeToClientName, PipeDirection.Out);

                var pipeFromClientName = pipeName + "_FromClient";
                var pipeFromClient = NamedPipeUtils.CreateNamedPipe(pipeFromClientName, PipeDirection.In);

                Console.WriteLine($"Waiting for simplex pipe connections, {pipeToClientName} & {pipeFromClientName}");

                pipeToClient.WaitForConnection();
                pipeFromClient.WaitForConnection();

                Console.WriteLine("Pipes connected!");

                var loggingInputStream = new LoggingStream(pipeFromClient, LoggingStream_OnRead, LoggingStream_OnWrite);
                var loggingOutputStream = new LoggingStream(pipeToClient, LoggingStream_OnRead, LoggingStream_OnWrite);

                input = loggingInputStream;
                output = loggingOutputStream;
            }
            return;
        }
        else if (Uri.TryCreate(connectionString, UriKind.Absolute, out var connectionUri))
        {
            if (connectionUri.Scheme == "tcp")
            {

            }
        }
        else if (connectionString.Equals("std", StringComparison.InvariantCultureIgnoreCase))
        {
            input = Console.OpenStandardInput();
            output = Console.OpenStandardOutput();
            return;
        }

        throw new ArgumentException("Invalid connection string", nameof(connectionString));
    }

    private static void LoggingStream_OnWrite(object? sender, ArraySegment<byte> obj)
    {
        try
        {
            var str = Encoding.UTF8.GetString(obj.Array!, obj.Offset, obj.Count);
            System.Diagnostics.Debug.WriteLine($"Sent `{str}`");
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Sent {obj.Count} bytes");
        }
    }

    private static void LoggingStream_OnRead(object? sender, ArraySegment<byte> obj)
    {
        try
        {
            var str = Encoding.UTF8.GetString(obj.Array!, obj.Offset, obj.Count);
            System.Diagnostics.Debug.WriteLine($"Received `{str}`");
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Received {obj.Count} bytes");
        }
    }

    public static void ConnectToString(string connectionString, out Stream input, out Stream output)
    {
        var pipeName = NamedPipeUtils.GetPipeName(connectionString);

        var pipeToServer = new NamedPipeClientStream(".", pipeName + "_FromClient", PipeDirection.Out, PipeOptions.Asynchronous);
        var pipeFromServer = new NamedPipeClientStream(".", pipeName + "_ToClient", PipeDirection.In, PipeOptions.Asynchronous);

        pipeToServer.Connect();
        pipeFromServer.Connect();

        var loggingInputStream = new LoggingStream(pipeFromServer, LoggingStream_OnRead, LoggingStream_OnWrite);
        var loggingOutputStream = new LoggingStream(pipeToServer, LoggingStream_OnRead, LoggingStream_OnWrite);

        input = loggingInputStream;
        output = loggingOutputStream;
    }
}

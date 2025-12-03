using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Microsoft.Iris.DebugAdapter;

public static class ConnectionStringHelper
{
    public static void CreateFromString(string connectionString, out Stream input, out Stream output)
    {
        // TODO: Support other transports
        if (NamedPipeUtils.TryGetPipeName(connectionString, out var pipeName))
        {
            var pipeToClient = NamedPipeUtils.CreateNamedPipe(pipeName + "_ToClient", PipeDirection.Out);
            var pipeFromClient = NamedPipeUtils.CreateNamedPipe(pipeName + "_FromClient", PipeDirection.In);

            pipeToClient.WaitForConnection();
            pipeFromClient.WaitForConnection();

            var loggingInputStream = new LoggingStream(pipeFromClient, LoggingStream_OnRead, LoggingStream_OnWrite);
            var loggingOutputStream = new LoggingStream(pipeToClient, LoggingStream_OnRead, LoggingStream_OnWrite);

            input = loggingInputStream;
            output = loggingOutputStream;
            return;
        }
        else if (Uri.TryCreate(connectionString, UriKind.Absolute, out var connectionUri))
        {
            if (connectionUri.Scheme == "tcp")
            {

            }
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

using System.IO;
using System.IO.Pipes;

namespace Microsoft.Iris.DebugAdapter;

public static class ConnectionStringHelper
{
    private const string PIPE_PREFIX = @"\\.\pipe\";

    public static void CreateFromString(string connectionString, out Stream input, out Stream output)
    {
        // TODO: Support other transports
        var pipeName = connectionString.StartsWith(PIPE_PREFIX)
            ? connectionString
            : NamedPipeUtils.GenerateValidNamedPipeName();

        var pipe = NamedPipeUtils.CreateNamedPipe(pipeName, PipeDirection.InOut);
        pipe.WaitForConnection();

        output = input = pipe;
    }

    public static void ConnectToString(string connectionString, out Stream input, out Stream output)
    {
        if (!connectionString.StartsWith(PIPE_PREFIX))
            throw new System.NotSupportedException();

        var pipe = new NamedPipeClientStream(connectionString);
        pipe.Connect();

        output = input = pipe;
    }
}

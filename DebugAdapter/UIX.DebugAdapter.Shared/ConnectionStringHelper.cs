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

        input = NamedPipeUtils.CreateNamedPipe(pipeName, PipeDirection.InOut);
        output = input;
    }
}

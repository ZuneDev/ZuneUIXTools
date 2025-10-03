using CommandLine;

namespace IrisLanguageServer;

public class CommandLineOptions
{
    [Option("pipe", Required = false, HelpText = "The named pipe to connect to for LSP communication")]
    public string? Pipe { get; set; }

    [Option("socket", Required = false, HelpText = "The TCP port to connect to for LSP communication")]
    public int? Socket { get; set; }

    [Option("stdio", Required = false, HelpText = "If set, use stdin/stdout for LSP communication")]
    public bool Stdio { get; set; }

    [Option("wait-for-debugger", Required = false, HelpText = "If set, wait for a dotnet debugger to be attached before starting the server")]
    public bool WaitForDebugger { get; set; }

    [Option("dap", Required = false, HelpText = "If set, run the server as a debug adapter. Otherwise, a language server is run.")]
    public bool Dap { get; set; }

    [Option("vs-compatibility-mode", Required = false, HelpText = "If set, runs in a mode to better support Visual Studio as a host")]
    public bool VsCompatibilityMode { get; set; }
}

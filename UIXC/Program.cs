using Spectre.Console.Cli;
using UIXC.Commands;

namespace UIXC;

internal class Program
{
    static int Main(string[] args)
    {
        var app = new CommandApp<CompileCommand>();

        app.Configure(config =>
        {
#if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
#endif
        });

        return app.Run(args);
    }
}

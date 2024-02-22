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
            config.AddCommand<CompileCommand>("compile")
                .WithAlias("c")
                .WithDescription("Compiles the given source to UIB.");

            config.AddCommand<DecompileCommand>("decompile")
                .WithAlias("d")
                .WithDescription("Decompiles the given compiled UIX to a source langauge.");

#if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
#endif
        });

        return app.Run(args);
    }
}

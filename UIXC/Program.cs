using Errata;
using Microsoft.Iris;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using Spectre.Console;

namespace UIXC;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Missing arguments: must specify a file to compile. Accepts XML and Asm source.[/]");
            return -1;
        }

        // TODO: Use Spectre.Console.Cli
        string sourcePath = args[0];
        CompilerInput compiland = new()
        {
            SourceFileName = sourcePath,
            OutputFileName = Path.Combine(Environment.CurrentDirectory, Path.ChangeExtension(sourcePath, ".uib"))
        };
        CompilerInput[] compilands = [compiland];

        // Configure error and warning messages
        var report = new Report(new IrisSourceRepository(compilands, default));
        ErrorManager.OnErrors += (errors) =>
        {
            foreach (ErrorRecord error in errors)
            {
                var l = Math.Max(0, error.Line);
                var c = Math.Max(0, error.Column);
                var msg = error.Message;
                var ctx = error.Context;

                const string compilerError = "Iris error";
                var diagnostic = error.Warning ? Diagnostic.Warning(compilerError) : Diagnostic.Error(compilerError);

                report.AddDiagnostic(diagnostic
                    .WithCode("UIX0000")
                    .WithLabel(new(ctx, new Location(l, c), msg))
                );
            }
        };
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);
        TraceSettings.Current.OnWriteLine += (line) =>
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]{line}[/]");
        };

        MarkupSystem.Startup(true);
        Assembler.RegisterLoader();

        // TODO: Support shared binary data tables
        var success = MarkupCompiler.Compile(compilands, default);

        if (!success)
        {
            AnsiConsole.MarkupLine("[red]Compilation failed.[/]");
            return -1;
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Compilation finished.[/]");
            return 0;
        }
    }
}

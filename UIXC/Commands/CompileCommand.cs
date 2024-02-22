using Errata;
using Microsoft.Iris;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace UIXC.Commands;

public class CompileCommand : Command<CompileCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Compilands.Length <= 0)
        {
            Diagnostic.Error("Missing arguments: must specify a file to compile. Accepts XML and Asm source.");
            //AnsiConsole.MarkupLine("[red][/]");
            return -1;
        }

        // TODO: Support multiple compilands
        string sourcePath = settings.Compilands[0];
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

    public sealed class Settings : CommandSettings
    {
        [Description("The source files to compile.")]
        [CommandArgument(0, "[compilands]")]
        public required string[] Compilands { get; init; }

        [Description("Directories to search for imported files in.")]
        [CommandOption("-I <VALUES>")]
        public required string[] IncludeDirectories { get; init; }

        [Description("A shared binary data table to compile with.")]
        [CommandOption("-t|--dataTable")]
        public string? DataTable { get; init; }

        [CommandOption("--verbose")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }
    }
}

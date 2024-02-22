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
        if (settings.Compilands is null || settings.Compilands.Length <= 0)
        {
            AnsiConsole.MarkupLine("[red]Missing arguments: must specify a file to compile. Accepts XML and Asm source.[/]");
            return -1;
        }

        CompilerInput[] compilands = new CompilerInput[settings.Compilands.Length];
        for (int c = 0; c < settings.Compilands.Length; c++)
        {
            string sourcePath = settings.Compilands[0];
            compilands[c] = new()
            {
                SourceFileName = sourcePath,
                OutputFileName = SourceToCompiledPath(settings, sourcePath),
            };
        }

        CompilerInput dataTableInput = new();
        if (settings.DataTable is not null)
        {
            dataTableInput.SourceFileName = settings.DataTable;
            dataTableInput.OutputFileName = SourceToCompiledPath(settings, settings.DataTable);
        }

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

        var success = MarkupCompiler.Compile(compilands, dataTableInput);

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

    private static string SourceToCompiledPath(Settings settings, string sourceFilePath)
    {
        var outputDir = settings.OutputDir ?? Environment.CurrentDirectory;
        return Path.Combine(outputDir, Path.ChangeExtension(sourceFilePath, ".uib"));
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

        [Description("The directory to output to.")]
        [CommandOption("-o|--output <outputDir>")]
        public string? OutputDir { get; init; }

        [CommandOption("--verbose")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }
    }
}

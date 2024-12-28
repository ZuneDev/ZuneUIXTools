using Microsoft.Iris;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace UIXC.Commands;

public class CompileCommand : CompilerCommandBase<CompileCommand.Settings>
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
            string sourcePath = settings.Compilands[c];
            compilands[c] = new()
            {
                SourceFileName = sourcePath,
                OutputFileName = SourceToCompiledPath(settings, sourcePath),
            };
        }

        CompilerInput? dataTableInput = null;
        if (settings.DataTable is not null)
        {
            dataTableInput = new()
            {
                SourceFileName = settings.DataTable,
                OutputFileName = SourceToCompiledPath(settings, settings.DataTable)
            };
        }

        // Configure error and warning messages
        BeginErrorReporting(new IrisSourceRepository(compilands, dataTableInput));
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Tool, byte.MaxValue);
        TraceSettings.Current.OnWriteLine += (line) =>
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]{line}[/]");
        };

        MarkupSystem.Startup(true);
        Assembler.RegisterLoader();

        System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(@"C:\Program Files\Zune\UIXcontrols.dll");
        var success = MarkupCompiler.Compile(compilands, dataTableInput ?? default);

        StopErrorReporting();
        Report?.Render(AnsiConsole.Console);
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

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

    public sealed class Settings : CompilerSettings
    {
        [Description("The source files to compile.")]
        [CommandArgument(0, "[compilands]")]
        public required string[] Compilands { get; init; }
    }
}

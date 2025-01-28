using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace UIXC.Commands;

public class DecompileCommand : CompilerCommandBase<DecompileCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Inputs is null || settings.Inputs.Length <= 0)
        {
            AnsiConsole.MarkupLine("[red]Missing arguments: must specify a UIB file to decompile.[/]");
            return -1;
        }

        if (settings.Verbose)
        {
            // Configure error and warning messages
            TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
            TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);
            TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupDebug, byte.MaxValue);
            TraceSettings.Current.OnWriteLine += (line) =>
            {
                AnsiConsole.MarkupLineInterpolated($"[grey]{line}[/]");
            };
        }

        BeginErrorReporting(new IrisSourceRepository(settings.Inputs));

        var loadAssembliesResult = LoadAssemblies(settings);
        if (loadAssembliesResult < 0)
            return loadAssembliesResult;

        foreach (var redirectOption in settings.ImportRedirects ?? [])
        {
            var parts = redirectOption.Split('>');
            MarkupSystem.AddImportRedirect(parts[0], parts[1]);
        }

        MarkupSystem.Startup(true);
        bool success = false;

        // Load the shared data table if one was specified
        LoadResult? dataTableLoadResult = null;
        if (settings.DataTable is not null)
        {
            dataTableLoadResult = LoadIrisFile(settings.DataTable, settings);
            if (dataTableLoadResult.Status != LoadResultStatus.Success)
                throw new Exception($"Failed to load data table from {dataTableLoadResult.ErrorContextUri}");
        }

        if (settings.Language == SourceLanguage.Asm)
        {
            foreach (var input in settings.Inputs)
            {
                try
                {
                    var uibLoadResult = LoadIrisFile(input, settings);
                    if (uibLoadResult.Status != LoadResultStatus.Success)
                        throw new Exception($"Failed to load UIB source from {uibLoadResult.ErrorContextUri}");

                    var disassembler = Disassembler.Load(uibLoadResult);
                    var asm = disassembler.Write();

                    File.WriteAllText(GetOutputPath(settings, input), asm);
                    success = true;
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(ex);
                    success = false;
                }
            }
        }
        else if (settings.Language == SourceLanguage.Xml)
        {
            AnsiConsole.MarkupLine("[red]UIX XML is not currently a supported decompilation target.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]{settings.Language} is not currently a supported decompilation target.[/]");
        }

        if (!success)
        {
            AnsiConsole.MarkupLine("[red]Decompilation failed.[/]");
            return -1;
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Decompilation finished.[/]");
            AnsiConsole.MarkupLine($"[green]Output saved to '{settings.OutputDir}'.[/]");
            return 0;
        }
    }

    private static string GetOutputPath(Settings settings, string inputFilePath)
    {
        var fileName = Path.GetFileName(inputFilePath);//.Replace('!', '/');
        var outputFile = Path.Combine(settings.OutputDir, Path.ChangeExtension(fileName, settings.Language.GetExtension()));

        var outputDir = Path.GetDirectoryName(outputFile)!;
        Directory.CreateDirectory(outputDir);

        return outputFile;
    }

    private LoadResult LoadIrisFile(string input, Settings settings)
    {
        if (!TryResolvePath(input, GetSearchPaths(settings), out var inputPath))
            inputPath = input;

        if (!inputPath.Contains("://"))
            inputPath = $"file://{inputPath}";

        var uibLoadResult = MarkupSystem.Load(inputPath, (uint)Random.Shared.Next());
        uibLoadResult.FullLoad();
        return uibLoadResult;
    }

    public sealed class Settings : CompilerSettings
    {
        [Description("The UIB files to decompile.")]
        [CommandArgument(0, "[inputs]")]
        public required string[] Inputs { get; init; }

        // TODO: Enum options
        [Description("The language to decompile to.")]
        [CommandOption("-l|--lang <lang>")]
        public SourceLanguage Language { get; init; } = SourceLanguage.Asm;
    }
}

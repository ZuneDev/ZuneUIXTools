using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.DecompXml;
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

        List<string> enumeratedInputs = new(settings.Inputs.Length);
        foreach (var inputPath in settings.Inputs)
        {
            if (!Directory.Exists(inputPath))
            {
                enumeratedInputs.Add(inputPath);
            }
            else
            {
                var compilableFiles = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories)
                    .Where(File.Exists)
                    .Select(f => f.ToLowerInvariant())
                    .Where(f => f.EndsWith(".uixa") || f.EndsWith(".uix") || f.EndsWith(".uib"));
                enumeratedInputs.AddRange(compilableFiles);
            }
        }

        var repoSources = enumeratedInputs.ToList();
        if (settings.DataTable is not null)
            repoSources.Add(settings.DataTable);
        BeginErrorReporting(new IrisSourceRepository(repoSources));

        var loadAssembliesResult = LoadAssemblies(settings);
        if (loadAssembliesResult < 0)
            return loadAssembliesResult;

        foreach (var redirectOption in settings.ImportRedirects ?? [])
        {
            var parts = redirectOption.Split('>');
            MarkupSystem.AddImportRedirect(parts[0], parts[1]);
        }

        MarkupSystem.Startup(true);
        Assembler.RegisterLoader();
        bool success = false;

        // Load the shared data table if one was specified
        LoadResult? dataTableLoadResult = null;
        if (settings.DataTable is not null)
        {
            dataTableLoadResult = LoadIrisFile(settings.DataTable, settings);
            if (dataTableLoadResult.Status != LoadResultStatus.Success)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Failed to load shared data table from {dataTableLoadResult.ErrorContextUri}[/]");
                goto done;
            }
        }

        Func<LoadResult, string> decompilerMethod; 

        if (settings.Language == SourceLanguage.Asm)
        {
            decompilerMethod = loadResult =>
            {
                var disassembler = Disassembler.Load(loadResult);
                return disassembler.Write();
            };
        }
        else if (settings.Language == SourceLanguage.Xml)
        {
            decompilerMethod = loadResult =>
            {
                var decompiler = Decompiler.Load(loadResult);
                return decompiler.DecompileToSource();
            };
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]{settings.Language} is not currently a supported decompilation target.[/]");
            goto done;
        }

        foreach (var input in enumeratedInputs)//.Where(f => !f.EndsWith(settings.Language.GetExtension())))
        {
            try
            {
                var uibLoadResult = LoadIrisFile(input, settings);
                if (uibLoadResult.Status != LoadResultStatus.Success)
                    throw new Exception($"Failed to load UIB source from {uibLoadResult.ErrorContextUri}");

                var decompiledSource = decompilerMethod(uibLoadResult);

                var output = GetOutputPath(settings, input);
                File.WriteAllText(output, decompiledSource);

                AnsiConsole.MarkupLineInterpolated($"[green]Decompiled to '{output}'[/]");
                success = true;
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                success = false;
            }
        }

    done:
        StopErrorReporting();
        Report?.Render(AnsiConsole.Console);
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

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
        var fileName = Path.GetFileNameWithoutExtension(inputFilePath);//.Replace('!', '/');
        var newFileName = $"{fileName}_decomp{settings.Language.GetExtension()}";
        var outputFile = Path.Combine(settings.OutputDir, newFileName);

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

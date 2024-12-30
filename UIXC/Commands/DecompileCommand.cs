using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

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

        var searchPaths = settings.IncludeDirectories.Prepend(Environment.CurrentDirectory).ToArray();

        foreach (var givenPath in settings.Assemblies ?? [])
        {
            try
            {
                var assemblyPath = ResolvePath(givenPath, searchPaths);
                _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                if (!AnsiConsole.Confirm("Do you want to continue?", false))
                    return -1;
            }
        }

        foreach (var redirectOption in settings.ImportRedirects ?? [])
        {
            var parts = redirectOption.Split('>');
            MarkupSystem.AddImportRedirect(parts[0], parts[1]);
        }

        MarkupSystem.Startup(true);
        bool success = false;

        if (settings.Language == SourceLanguage.Asm)
        {
            foreach (var input in settings.Inputs)
            {
                try
                {
                    if (!TryResolvePath(input, searchPaths, out var inputPath))
                        inputPath = input;

                    if (!inputPath.Contains("://"))
                        inputPath = $"file://{inputPath}";

                    var uibLoadResult = MarkupSystem.Load(inputPath, (uint)Random.Shared.Next());
                    uibLoadResult.FullLoad();

                    if (uibLoadResult.Status != LoadResultStatus.Success)
                        throw new Exception($"Failed to load UIB source ({uibLoadResult.ErrorContextUri})");

                    var disassembler = Disassembler.Load(uibLoadResult as MarkupLoadResult);
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

        var outputDir = Path.GetDirectoryName(outputFile);
        Directory.CreateDirectory(outputDir);

        return outputFile;
    }

    private static string ResolvePath(string givenPath, ICollection<string> searchPaths)
    {
        if (!TryResolvePath(givenPath, searchPaths, out var assemblyPath))
            throw new FileNotFoundException(null, givenPath);
        return assemblyPath;
    }

    private static bool TryResolvePath(string givenPath, ICollection<string> searchPaths, [NotNullWhen(true)] out string? absolutePath)
    {
        if (Path.IsPathFullyQualified(givenPath))
        {
            absolutePath = givenPath;
            return Path.Exists(absolutePath);
        }

        absolutePath = null;
        if (searchPaths.Count == 0)
            return false;
        
        foreach (var searchPath in searchPaths)
        {
            absolutePath = Path.GetFullPath(givenPath, searchPath);
            if (Path.Exists(absolutePath))
                return true;
        }

        return false;
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

using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace UIXC.Commands;

public class DecompileCommand : Command<DecompileCommand.Settings>
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

        MarkupSystem.Startup(true);
        bool success = false;

        if (settings.Language == SourceLanguage.Asm)
        {
            foreach (var input in settings.Inputs)
            {
                try
                {
                    var uibLoadResult = MarkupSystem.Load($"file://{input}", (uint)Random.Shared.Next());
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
            return 0;
        }
    }
    private static string GetOutputPath(Settings settings, string inputFilePath)
    {
        var outputDir = settings.OutputDir ?? Environment.CurrentDirectory;
        return Path.Combine(outputDir, Path.ChangeExtension(inputFilePath, settings.Language.GetExtension()));
    }

    public sealed class Settings : CommandSettings
    {
        [Description("The UIB files to decompile.")]
        [CommandArgument(0, "[inputs]")]
        public required string[] Inputs { get; init; }

        [Description("Directories to search for imported files in.")]
        [CommandOption("-I <VALUES>")]
        public required string[] IncludeDirectories { get; init; }

        [Description("A shared binary data table to decompile with.")]
        [CommandOption("-t|--dataTable")]
        public string? DataTable { get; init; }

        [Description("The directory to output to.")]
        [CommandOption("-o|--output <outputDir>")]
        public string? OutputDir { get; init; }

        // TODO: Enum options
        [Description("The language to decompile to.")]
        [CommandOption("-l|--lang <lang>")]
        public SourceLanguage Language { get; init; } = SourceLanguage.Asm;

        [CommandOption("--verbose")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }
    }
}

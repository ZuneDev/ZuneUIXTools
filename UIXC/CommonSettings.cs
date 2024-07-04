using Spectre.Console.Cli;
using System.ComponentModel;

namespace UIXC;

public abstract class CommonSettings : CommandSettings
{
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }
}

public abstract class CompilerSettings : CommonSettings
{
    [Description("The directory to output to.")]
    [CommandOption("-o|--output <outputDir>")]
    public string? OutputDir { get; init; } = Environment.CurrentDirectory;

    [Description("The shared binary data table to use.")]
    [CommandOption("-t|--dataTable")]
    public string? DataTable { get; init; }

    [Description("Directories to search for imported files in.")]
    [CommandOption("-I <VALUES>")]
    public required string[] IncludeDirectories { get; init; }

    [Description("External assemblies to load.")]
    [CommandOption("-A <VALUES>")]
    public required string[] Assemblies { get; init; }

    [Description("Redirects imports.")]
    [CommandOption("--ir <VALUES>")]
    public required string[] ImportRedirects { get; init; }
}

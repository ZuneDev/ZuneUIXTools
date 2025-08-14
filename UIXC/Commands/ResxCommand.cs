using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Xml;
using System.Xml.Linq;

namespace UIXC.Commands;

public class ResxCommand : Command<ResxCommandSettings>
{
    public override int Execute(CommandContext context, ResxCommandSettings settings)
    {
        if (settings.InputDir is null)
        {
            AnsiConsole.MarkupLine("[red]Missing argument: must specify a directory to read resources from.[/]");
            return -1;
        }

        if (settings.OutputFile is null)
        {
            AnsiConsole.MarkupLine("[red]Missing argument: must specify a file to write RESX to.[/]");
            return -1;
        }

        var relativeSourceDir = Path.GetRelativePath(Path.GetDirectoryName(settings.OutputFile), settings.InputDir);

        var xRoot = XElement.Parse(RESX_TEMPLATE);
        var resourceCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(settings.InputDir))
        {
            var fileName = Path.GetFileName(filePath);
            var resourceName = fileName.ToUpperInvariant();
            var relativeFilePath = Path.Join(relativeSourceDir, fileName);

            var xValue = new XElement("value", $"{relativeFilePath};System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            var xData = new XElement("data", xValue);
            xData.SetAttributeValue("name", resourceName);
            xData.SetAttributeValue("type", "System.Resources.ResXFileRef, System.Windows.Forms");

            xRoot.Add(xData);

            resourceCount++;
        }

        using var outputFileStream = File.Open(settings.OutputFile, FileMode.Create);
        using var writer = XmlWriter.Create(outputFileStream);
        xRoot.WriteTo(writer);
        writer.Flush();

        AnsiConsole.MarkupLineInterpolated($"[green]Wrote {resourceCount} resources to '{settings.OutputFile}'[/]");

        return 0;
    }

    private const string RESX_TEMPLATE = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
            <resheader name="resmimetype">
                <value>text/microsoft-resx</value>
            </resheader>
            <resheader name="version">
                <value>2.0</value>
            </resheader>
            <resheader name="reader">
                <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
            </resheader>
            <resheader name="writer">
                <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
            </resheader>
            <assembly alias="System.Windows.Forms" name="System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" />
        </root>
        """;

    private const string RESX_ITEM_TEMPLATE = """
        <data name="ABOUTDIALOG" type="System.Resources.ResXFileRef, System.Windows.Forms">
        	<value>RCDATA\ABOUTDIALOG.uix;System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
        </data>
        """;
}

public class ResxCommandSettings : CommandSettings
{
    [Description("The directory to read resources from.")]
    [CommandOption("-i|--input <inputDir>")]
    public required string InputDir { get; init; } = Environment.CurrentDirectory;

    [Description("The file to output to.")]
    [CommandOption("-o|--output <outputFile>")]
    public required string OutputFile { get; init; } = Path.Combine(Environment.CurrentDirectory, "RCDATA.resx");
}

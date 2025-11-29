using Microsoft.Iris.Debug;
using Microsoft.Iris.Debug.Symbols;
using Microsoft.Iris.Debug.SystemNet;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Globalization;

namespace UIXC.Commands;

public class DebugCommand : Command<DebugCommand.Settings>
{
    private bool isRunning = true;

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.ServerUri is null)
        {
            AnsiConsole.MarkupLine("[red]Missing argument: must specify a debug server to connect to.[/]");
            return -1;
        }

        DebugSymbolResolver? symbolResolver = null;

        if (settings.SymbolDir is not null)
        {
            Directory.CreateDirectory(settings.SymbolDir);
            symbolResolver = new(settings.SymbolDir, settings.SourceDir);
        }

        AnsiConsole.MarkupLineInterpolated($"Connecting to '{settings.ServerUri}'...");
                
        var c = new NetDebuggerClient(settings.ServerUri);
        c.Connected += (s, e) =>
        {
            AnsiConsole.MarkupLine("[green]Connected[/]");
            Thread consoleThread = new(() => DebugConsole(c, symbolResolver));
            consoleThread.Start();
        };

        c.Start();

        while (isRunning) ;

        return 0;
    }

    private int DebugConsole(IDebuggerClient client, DebugSymbolResolver? symbolResolver)
    {
        client.InterpreterStateChanged += (cmd) =>
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Application in '{cmd}' state[/]");
        };

        while (true)
        {
            var input = AnsiConsole.Prompt(new TextPrompt<string>("> "));
            var inputParts = input.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var command = inputParts[0].ToUpperInvariant();
            switch (command)
            {
                case "BREAK" or "B":
                    string path = inputParts[1];
                    var fileName = Path.GetFileName(path).Split('!')[^1];

                    uint breakOffset;

                    if (inputParts.Length >= 3 && inputParts[2].StartsWith("0x"))
                    {
                        breakOffset = uint.Parse(inputParts[2][2..], NumberStyles.HexNumber);
                    }
                    else
                    {
                        if (symbolResolver is null)
                        {
                            AnsiConsole.MarkupLine("[red]Setting breakpoints via source code requires debug symbols. Use the --symbols argument to specify a directory.[/]");
                            break;
                        }

                        var fsym = symbolResolver.GetForFile(fileName);
                        if (fsym is null)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]No symbols loaded for '{fileName}'.[/]");
                            break;
                        }

                        var line = int.Parse(inputParts[2]);
                        var column = int.Parse(inputParts[3]);
                        var position = new SourcePosition(line, column);

                        var location = fsym!.SourceMap.GetLocationFromPosition(position);
                        if (location is null)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]Line {line}, column {column} has known offset in '{fileName}'.[/]");
                            break;
                        }

                        breakOffset = location.Value.Offset;

                        if (fsym.HasSourceCode())
                        {
                            var sourceCode = fsym.GetSourceSubstring(location.Value.Span);
                            AnsiConsole.Write(new Panel(sourceCode));
                        }
                    }

                    AnsiConsole.MarkupLineInterpolated($"[green]Breakpoint set in '{fileName}' at offset 0x{breakOffset:X4}.[/]");
                    client.UpdateBreakpoint(new(path, breakOffset));

                    break;

                case "CONTINUE" or "C":
                    client.DebuggerCommand = Microsoft.Iris.Debug.Data.InterpreterCommand.Continue;
                    break;

                case "CLEAR":
                    // Not implemented yet, should clear all breakpoints
                    break;

                case "EXIT":
                    isRunning = false;
                    return 0;
            }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description("The directory containing pre-generated symbols.")]
        [CommandOption("-s|--symbols <symbolDir>")]
        public string? SymbolDir { get; init; }

        [Description("The directory containing source code.")]
        [CommandOption("--sources <sourceDir>")]
        public string? SourceDir { get; init; }

        [Description("Whether to decompile the current file when a breakpoint is hit. Generated symbols will be written to the symbol directory if specified.")]
        [CommandOption("-d|--decompile")]
        public bool Decompile { get; init; }

        [Description("The URI of the debug server to connect to.")]
        [CommandOption("-u|--server <serverUri>")]
        public Uri ServerUri { get; init; } = DebugRemoting.DEFAULT_TCP_URI;
    }
}

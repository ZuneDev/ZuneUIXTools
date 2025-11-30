using Microsoft.Iris.Debug;
using Microsoft.Iris.Debug.Symbols;
using Microsoft.Iris.Debug.SystemNet;
using Microsoft.Iris.DebugAdapter.Client;
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
        if (settings.ConnectionString is null)
        {
            AnsiConsole.MarkupLine("[red]Missing argument, a connection string must be specified.[/]");
            return -1;
        }

        IDebuggerClient c;

        if (Uri.TryCreate(settings.ConnectionString, UriKind.Absolute, out var connectionUri))
        {
            if (connectionUri.Scheme == "tcp")
            {
                c = new NetDebuggerClient(connectionUri);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
        else
        {
            c = new IrisDebugAdapterClient(settings.ConnectionString);
        }

        DebugSymbolResolver? symbolResolver = null;

        if (settings.SymbolDir is not null)
        {
            Directory.CreateDirectory(settings.SymbolDir);
            symbolResolver = new(settings.SymbolDir, settings.SourceDir);
        }

        AnsiConsole.MarkupLineInterpolated($"Connecting to '{settings.ConnectionString}'...");

        ((IRemoteDebuggerState)c).Connected += (s, e) =>
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

                        if (fsym.OriginalFileName is not null)
                            path = fsym.OriginalFileName;

                        var line = int.Parse(inputParts[2]);
                        var column = int.Parse(inputParts[3]);
                        var position = new SourcePosition(line, column);

                        var location = fsym!.SourceMap.GetLocationFromPosition(position);
                        if (location is null)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]Line {line}, column {column} has known offset in '{fileName}'.[/]");
                            break;
                        }

                        breakOffset = location.Offset;

                        if (fsym.HasSourceCode())
                        {
                            var sourceCode = fsym.GetSourceSubstring(location.Span);
                            AnsiConsole.Write(new Panel(sourceCode.EscapeMarkup()));
                        }
                    }

                    client.UpdateBreakpoint(new(path, breakOffset));
                    AnsiConsole.MarkupLineInterpolated($"[green]Breakpoint set in '{path}' at offset 0x{breakOffset:X4}.[/]");

                    break;

                case "CONTINUE" or "C":
                    client.DebuggerCommand = Microsoft.Iris.Debug.Data.InterpreterCommand.Continue;
                    break;

                case "STEP" or "S":
                    client.DebuggerCommand = Microsoft.Iris.Debug.Data.InterpreterCommand.Step;
                    break;

                case "ENABLE":

                    break;

                case "CLEAR":
                    // Not implemented yet, should clear all breakpoints
                    break;

                case "EXIT" or "QUIT":
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

        [Description("The string used to connect to the debug server.")]
        [CommandOption("-u|--server <serverUri>")]
        public string ConnectionString { get; init; } = DebugRemoting.DEFAULT_TCP_URI.ToString();
    }
}

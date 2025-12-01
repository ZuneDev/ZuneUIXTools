using Microsoft.Iris.Debug;
using Microsoft.Iris.Debug.Data;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using IrisBreakpoint = Microsoft.Iris.Debug.Data.Breakpoint;

namespace Microsoft.Iris.DebugAdapter.Client;

public class IrisDebugAdapterClient : IDebuggerClient, IRemoteDebuggerState, IDisposable
{
    private Stream _inputStream;
    private Stream _outputStream;
    private DebugAdapterClient? _debugAdapter;
    private InterpreterCommand _debuggerCommand;

    public InterpreterCommand DebuggerCommand
    {
        get => _debuggerCommand;
        set
        {
            _debuggerCommand = value;

            _ = value switch
            {
                InterpreterCommand.Break => _debugAdapter.RequestPause(new()),
                InterpreterCommand.Continue => _debugAdapter.RequestContinue(new()),
                InterpreterCommand.Step => _debugAdapter.RequestNext(new()),
                _ => Task.CompletedTask
            };
        }
    }

    public string ConnectionString { get; }

    public event Action<InterpreterCommand> InterpreterStateChanged;
    public event EventHandler<InterpreterInstruction> InterpreterDecode;
    public event EventHandler<InterpreterEntry> InterpreterExecute;
    public event Action<string> DispatcherStep;
    public event Action<IDebuggerState, object> Connected;

    public IrisDebugAdapterClient(Stream inputStream, Stream outputStream)
    {
        _inputStream = inputStream;
        _outputStream = outputStream;
    }

    public IrisDebugAdapterClient(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public async Task StartAsync()
    {
        if (ConnectionString is not null)
        {
            ConnectionStringHelper.ConnectToString(ConnectionString, out _inputStream, out _outputStream);
        }

        _debugAdapter = await DebugAdapterClient.From(options =>
        {
            options
                .WithInput(_inputStream)
                .WithOutput(_outputStream)
                .OnInitialize((server, _, cancellationToken) =>
                {
                    var __ = server.RequestDebugAdapterInitialize(new());
                    return Task.CompletedTask;
                })
                .OnInitialized((_, _, response, _) =>
                {
                    Connected?.Invoke(this, EventArgs.Empty);
                    return Task.CompletedTask;
                })
                .OnContinued(args =>
                {
                    _debuggerCommand = InterpreterCommand.Continue;
                    InterpreterStateChanged?.Invoke(_debuggerCommand);
                })
                .OnStopped(args =>
                {
                    _debuggerCommand = InterpreterCommand.Break;
                    InterpreterStateChanged?.Invoke(_debuggerCommand);
                })
            ;
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _debugAdapter?.Dispose();
        _inputStream.Dispose();
        _outputStream.Dispose();
    }

    public void UpdateBreakpoint(IrisBreakpoint irisBreakpoint)
    {
        if (irisBreakpoint.Line > 0)
        {
            List<SourceBreakpoint> sourceBreakpoints = [
                new()
                {
                    Line = irisBreakpoint.Line,
                    Column = irisBreakpoint.Column > 0
                        ? irisBreakpoint.Column
                        : null
                }
            ];

            _ = _debugAdapter.SetBreakpoints(new()
            {
                Breakpoints = sourceBreakpoints
            });

        }
        else if (irisBreakpoint.Offset != uint.MaxValue)
        {
            // Instruction breakpoint
            List<InstructionBreakpoint> instructionBreakpoints = [
                new()
                {
                    InstructionReference = $"{irisBreakpoint.Uri}@0x{irisBreakpoint.Offset:X}",
                    //Offset = irisBreakpoint.Offset,
                }
            ];

            _ = _debugAdapter.SetInstructionBreakpoints(new()
            {
                Breakpoints = instructionBreakpoints
            });
        }
    }

    public void RequestLineNumberTable(string uri, Action<MarkupLineNumberEntry[]> callback)
    {
    }

    public void Start()
    {
        System.Threading.Thread clientThread = new(() =>
        {
            _ = StartAsync();
            //.ContinueWith(t =>
            //{
            //    if (t.Exception is not null)
            //        System.Diagnostics.Debug.WriteLine(t.Exception);
            //});
        });
        clientThread.IsBackground = true;
        clientThread.Start();
    }
}

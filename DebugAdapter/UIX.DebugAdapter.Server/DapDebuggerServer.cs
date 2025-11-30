using Microsoft.Iris.Debug;
using Microsoft.Iris.Debug.Data;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using System;

namespace Microsoft.Iris.DebugAdapter.Server;

public class DapDebuggerServer : IDebuggerServer, IRemoteDebuggerState
{
    private readonly IrisDebugServerOptions _debugAdapterOptions;
    private IrisDebugAdapterServer? _debugAdapter;
    private InterpreterCommand _debuggerCommand;

    public InterpreterCommand DebuggerCommand
    {
        get => _debuggerCommand;
        set
        {
            _debuggerCommand = value;

            if (_debugAdapter?.Server is null)
                return;

            if (value is InterpreterCommand.Continue)
            {
                _debugAdapter.Server.SendContinued(new()
                {
                    ThreadId = Environment.CurrentManagedThreadId,
                });
            }
            else if (value is InterpreterCommand.Break)
            {
                _debugAdapter.Server.SendStopped(new()
                {
                    Reason = new("unknown"),
                    ThreadId = Environment.CurrentManagedThreadId,
                });
            }
        }
    }

    public string ConnectionString { get; }

    public event Action<IDebuggerState, object>? Connected;

    public DapDebuggerServer(string connectionString, IrisDebugServerOptions options)
    {
        ConnectionString = connectionString;

        _debugAdapterOptions = options;
    }

    public void LogDispatcher(string message)
    {
    }

    public void LogInterpreterDecode(object context, InterpreterInstruction instruction)
    {
    }

    public void LogInterpreterExecute(object context, InterpreterEntry entry)
    {
    }

    public MarkupLineNumberEntry[] OnLineNumberTableRequested(string uri)
    {
        return [];
    }

    public void Start()
    {
        ConnectionStringHelper.CreateFromString(ConnectionString, out var input, out var output);

        _debugAdapter = new(input, output, _debugAdapterOptions);
        _debugAdapter.StartAsync().RunSynchronously();

        Connected?.Invoke(this, EventArgs.Empty);
    }

    public void WaitForContinue()
    {
        while (DebuggerCommand is InterpreterCommand.Break) ;
    }
}

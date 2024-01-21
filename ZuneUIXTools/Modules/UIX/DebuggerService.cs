using Gemini.Modules.Output;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Debug.Data;
using Microsoft.Iris.Debug.SystemNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace ZuneUIXTools.Modules.UIX;

[Export(typeof(DebuggerService))]
public class DebuggerService
{
    private readonly IOutput _output;
    private readonly ConcurrentDictionary<string, ConcurrentBag<InterpreterInstruction>> _entriesByFile = new();

    public event Action Stopped;

    [ImportingConstructor]
    public DebuggerService(IOutput output)
    {
        _output = output;
    }

    public bool IsRunning => Client != null;

    public IDebuggerClient Client { get; private set; }

    public IReadOnlyDictionary<string, ConcurrentBag<InterpreterInstruction>> ConstructedFiles => _entriesByFile;

    public bool IsInBreakMode { get; private set; }

    public void Start(string connectionUri = null)
    {
        Stop();

        Client = new NetDebuggerClient(connectionUri);
        Client.InterpreterDecode += ClientInterpreterDecode;
        Client.InterpreterExecute += ClientInterpreterExecute;

        _output.AppendLine($"Debugger listening on {Client.ConnectionUri}");
    }

    public void Stop()
    {
        _entriesByFile.Clear();

        if (Client is null)
            return;
        else if (Client is IDisposable disposable)
            disposable.Dispose();

        Client.InterpreterDecode -= ClientInterpreterDecode;
        Client.InterpreterExecute -= ClientInterpreterExecute;
        Client = null;

        Stopped?.Invoke();
        _output.AppendLine("Debugger disconnected");
    }

    public string PrintDisassembly(string uri)
    {
        if (!_entriesByFile.TryGetValue(uri, out var entries))
            throw new ArgumentException();

        var sortedEntries = entries.ToImmutableSortedSet();

        StringBuilder sb = new();
        sb.AppendJoin(Environment.NewLine, sortedEntries.Select(e => e.ToString()));
        return sb.ToString();
    }

    private void ClientInterpreterDecode(object sender, InterpreterInstruction currentInstruction)
    {
        _output.AppendLine("[UIX Dec] " + currentInstruction.ToString());

        var entries = _entriesByFile.GetOrAdd(currentInstruction.LoadUri, _ => new());

        if (entries.Any(e => e.Offset == currentInstruction.Offset))
            return;
        entries.Add(currentInstruction);
    }

    private void ClientInterpreterExecute(object sender, InterpreterEntry entry)
    {
        _output.AppendLine("[UIX Exe] " + entry.ToString());
    }
}

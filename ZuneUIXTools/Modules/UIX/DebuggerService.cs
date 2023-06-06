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
    private readonly ConcurrentDictionary<string, ConcurrentBag<InterpreterEntry>> _entriesByFile = new();
    private IDebuggerClient _client;

    public event Action Stopped;

    [ImportingConstructor]
    public DebuggerService(IOutput output)
    {
        _output = output;
    }

    public bool IsRunning => _client != null;

    public IDebuggerClient Client => _client;

    public IReadOnlyDictionary<string, ConcurrentBag<InterpreterEntry>> ConstructedFiles => _entriesByFile;

    public void Start(string connectionUri = null)
    {
        Stop();

        _client = new NetDebuggerClient(connectionUri);
        _client.InterpreterStep += Client_InterpreterStep;

        _output.AppendLine($"Debugger connected to {_client.ConnectionUri}");
    }

    public void Stop()
    {
        _entriesByFile.Clear();

        if (_client is null)
            return;
        else if (_client is IDisposable disposable)
            disposable.Dispose();

        _client.InterpreterStep -= Client_InterpreterStep;
        _client = null;

        Stopped?.Invoke();
        _output.AppendLine("Debugger disconnected");
    }

    public string PrintDisassembly(string uri)
    {
        if (!_entriesByFile.TryGetValue(uri, out var entries))
            throw new ArgumentException();

        var sortedEntries = entries.ToImmutableSortedSet();

        StringBuilder sb = new();
        sb.AppendJoin(Environment.NewLine, sortedEntries.Select(e => e.ToInstructionString()));
        return sb.ToString();
    }

    private void Client_InterpreterStep(object sender, InterpreterEntry currentEntry)
    {
        var entries = _entriesByFile.GetOrAdd(currentEntry.LoadUri, _ => new ConcurrentBag<InterpreterEntry>());

        if (entries.Any(e => e.Offset == currentEntry.Offset))
            return;
        entries.Add(currentEntry);
    }
}

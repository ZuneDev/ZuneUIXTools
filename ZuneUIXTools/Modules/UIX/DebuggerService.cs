using Gemini.Modules.Output;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Debug.SystemNet;
using System;
using System.ComponentModel.Composition;

namespace ZuneUIXTools.Modules.UIX;

[Export(typeof(DebuggerService))]
public class DebuggerService
{
    private readonly IOutput _output;
    private IDebuggerClient _client;

    public event Action Stopped;

    [ImportingConstructor]
    public DebuggerService(IOutput output)
    {
        _output = output;
    }

    public bool IsRunning => _client != null;

    public IDebuggerClient Client => _client;

    public void Start(string connectionUri = null)
    {
        Stop();

        _client = new NetDebuggerClient(connectionUri);

        _output.AppendLine($"Debugger connected to {_client.ConnectionUri}");
    }

    public void Stop()
    {
        if (_client == null)
            return;

        if (_client is IDisposable disposable)
            disposable.Dispose();

        Stopped?.Invoke();
        _client = null;
        _output.AppendLine("Debugger disconnected");
    }
}

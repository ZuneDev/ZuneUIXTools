using Gemini.Modules.Output;
using Microsoft.Iris.Debug;
using System;
using System.ComponentModel.Composition;

namespace ZuneUIXTools.Modules.UIX;

[Export(typeof(DebuggerService))]
public class DebuggerService
{
    private readonly IOutput _output;
    private IDebuggerClient _client;

    [ImportingConstructor]
    public DebuggerService(IOutput output)
    {
        _output = output;
    }

    public bool IsRunning => _client != null;

    public void Start(string connectionUri)
    {
        Stop();

        _client = new ZmqDebuggerClient(connectionUri);
        _client.DispatcherStep += Client_DispatcherStep;
    }

    private void Client_DispatcherStep(string obj)
    {
        _output?.AppendLine($"[Dispatcher] {obj}");
    }

    public void Stop()
    {
        if (_client == null)
            return;

        if (_client is IDisposable disposable)
            disposable.Dispose();

        _client.DispatcherStep -= Client_DispatcherStep;
        _client = null;
    }
}

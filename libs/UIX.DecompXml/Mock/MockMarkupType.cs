using Microsoft.Iris.Library;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using System;
using System.Collections.Generic;

namespace Microsoft.Iris.DecompXml.Mock;

internal class MockMarkupType : IMarkupTypeBase
{
    private static readonly DeferredHandler s_executePendingScriptsHandler = ExecutePendingScripts;

    private readonly Dictionary<string, object> _propertyStorage = [];
    private readonly Dictionary<SymbolReference, object> _symbolStorage = [];
    private readonly MarkupTypeSchema _typeSchema;

    private List<IDisposableObject> _disposables = null;
    private ScriptRunScheduler _scriptRunScheduler = new();

    public MockMarkupType(MarkupTypeSchema typeSchema)
    {
        _typeSchema = typeSchema;
    }

    public TypeSchema TypeSchema => _typeSchema;

    public MarkupListeners Listeners { get; set; }

    public bool ScriptEnabled => true;

    public Dictionary<object, object> Storage { get; } = [];

    public IReadOnlyDictionary<string, object> Properties => _propertyStorage;

    public IReadOnlyDictionary<SymbolReference, object> SymbolReferences => _symbolStorage;

    public object GetProperty(string name) => _propertyStorage[name];

    public void SetProperty(string name, object value) => _propertyStorage[name] = value;

    public void NotifyInitialized() { }

    public void NotifyScriptErrors() { }

    public object ReadSymbol(SymbolReference symbolRef) => _symbolStorage[symbolRef];

    public void WriteSymbol(SymbolReference symbolRef, object value) => _symbolStorage[symbolRef] = value;

    public object RunScript(uint scriptId, bool ignoreErrors, ParameterContext parameterContext) => _typeSchema.Run(this, scriptId, ignoreErrors, parameterContext);

    public void ScheduleScriptRun(uint scriptId, bool ignoreErrors)
    {
        if (!_scriptRunScheduler.Pending)
            DeferredCall.Post(DispatchPriority.Script, s_executePendingScriptsHandler, this);
        _scriptRunScheduler.ScheduleRun(scriptId, ignoreErrors);
    }

    private static void ExecutePendingScripts(object args)
    {
        var mockType = (MockMarkupType)args;
        mockType._scriptRunScheduler.Execute(mockType);
    }

    public void RegisterDisposable(IDisposableObject disposable)
    {
        _disposables ??= [];
        _disposables.Add(disposable);
    }

    public bool UnregisterDisposable(ref IDisposableObject disposable)
    {
        if (_disposables != null)
        {
            int index = _disposables.IndexOf(disposable);
            if (index != -1)
            {
                disposable = _disposables[index];
                _disposables.RemoveAt(index);
                return true;
            }
        }
        return false;
    }
}

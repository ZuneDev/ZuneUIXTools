using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Inspector;
using Microsoft.Iris.Debug.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using WpfHexaEditor.Core.MethodExtention;
using ZuneUIXTools.Modules.UIX;

namespace ZuneUIXTools.Modules.UIXCompiled;

[Export(typeof(UIBDisassemblyViewModel))]
public class UIBDisassemblyViewModel : Document
{
    readonly DebuggerService _debuggerService;
    readonly IShell _shell;
    UIBDisassemblyView _view;

    public ObservableCollection<InterpreterEntry> Instructions { get; } = new();

    [ImportingConstructor]
    public UIBDisassemblyViewModel(DebuggerService debuggerService, IShell shell)
    {
        _debuggerService = debuggerService;
        _debuggerService.Client.InterpreterStep += Client_InterpreterStep;

        _shell = shell;

        DisplayName = $"UIB Disassembler ('{_debuggerService.Client.ConnectionUri}')";
    }

    private void Client_InterpreterStep(object sender, InterpreterEntry entry)
    {
        _view?.Dispatcher.Invoke(() =>
        {
            Instructions.Add(entry);
        });
    }

    protected override void OnViewReady(object view)
    {
        base.OnViewReady(view);

        _view = (UIBDisassemblyView)view;
    }

    public void SetInspector(object objToInspect)
    {
        var inspector = IoC.Get<IInspectorTool>();
        inspector.SelectedObject = new InspectableObjectBuilder()
            .WithObjectProperties(objToInspect, _ => true)
            .ToInspectableObject();

        // TODO: Create IEditor for collections

        _shell.ShowTool(inspector);
    }
}

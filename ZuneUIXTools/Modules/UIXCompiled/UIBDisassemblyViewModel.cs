using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Inspector;
using Gemini.Modules.Inspector.Conventions;
using Gemini.Modules.Inspector.Inspectors;
using Microsoft.Iris.Debug.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using ZuneUIXTools.Converters;
using ZuneUIXTools.Modules.Inspectors;
using ZuneUIXTools.Modules.UIX;

namespace ZuneUIXTools.Modules.UIXCompiled;

[Export(typeof(UIBDisassemblyViewModel))]
public class UIBDisassemblyViewModel : Document
{
    readonly DebuggerService _debuggerService;
    readonly IShell _shell;
    UIBDisassemblyView _view;
    bool _autoScroll;

    public ObservableCollection<InterpreterEntry> Instructions { get; } = new();

    public bool AutoScroll
    {
        get => _autoScroll;
        set => Set(ref _autoScroll, value);
    }

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

    public void SetInspector(InterpreterEntry entry)
    {
        var props = (from PropertyDescriptor x in TypeDescriptor.GetProperties(entry) where x.IsBrowsable select x).ToList();

        DefaultPropertyInspectors.InspectorBuilders.Add(new EnumerablePropertyEditorBuilder());

        var inspector = IoC.Get<IInspectorTool>();
        inspector.SelectedObject = new InspectableObjectBuilder()
            .WithObjectProperties(entry, obj => !(obj.Name == nameof(entry.Parameters) || obj.Name == nameof(entry.ReturnValues)))
            .WithCollapsibleGroup("Parameters",
                b => b.WithEditor(entry, e => e.Parameters, new EnumerableEditorViewModel(InterpreterObjectBuilder)))
            .WithCollapsibleGroup("Return values",
                b => b.WithEditor(entry, e => e.ReturnValues, new EnumerableEditorViewModel(InterpreterObjectBuilder)))
            .ToInspectableObject();

        _shell.ShowTool(inspector);
    }

    private static InspectableObject InterpreterObjectBuilder(object obj)
    {
        if (obj is not InterpreterObject instance)
            throw new ArgumentException(null, nameof(obj));

        bool filter(PropertyDescriptor prop)
        {
            if (prop.Name == nameof(instance.TableIndex) && instance.TableIndex == -1)
                return false;
            else if (prop.Name == nameof(instance.Value))
                return false;

            return prop.GetValue(instance) != null;
        }

        InspectableObjectBuilder builder = new InspectableObjectBuilder()
            .WithObjectProperties(instance, filter);

        var valueType = instance.Value?.GetType() ?? instance.Type;
        if (valueType is not null)
        {
            PropertyDescriptor valueProperty = TypeDescriptor.CreateProperty(typeof(InterpreterObject), "Value", valueType);
            IEditor editor = DefaultPropertyInspectors.CreateEditor(valueProperty);
            if (editor is null)
            {
                builder = builder.WithCollapsibleGroup(nameof(instance.Value), b =>
                    b.WithObjectProperties(instance.Value, _ => true));
            }
            else
            {
                var converterProp = editor.GetType().GetProperty("Converter");
                converterProp?.SetValue(editor, new CastValueConverter());

                editor.BoundPropertyDescriptor = new BoundPropertyDescriptor(instance, valueProperty);
                builder = builder.WithEditor(instance, i => i.Value, editor);
            }
        }

        return builder.ToInspectableObject();
    }
}

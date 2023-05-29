using Gemini.Modules.Inspector;
using Gemini.Modules.Inspector.Inspectors;
using Gemini.Modules.Inspector.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ZuneUIXTools.Modules.Inspectors;

public class EnumerableEditorViewModel : EditorBase<IEnumerable>, ILabelledInspector
{
    public EnumerableEditorViewModel() : this(null) { }

    public EnumerableEditorViewModel(Func<object, IInspectableObject> builder)
    {
        Builder = builder ?? DefaultBuilder;
    }

    public Func<object, IInspectableObject> Builder { get; }

    public override bool IsReadOnly => Value is IList collection && collection.IsReadOnly;

    public IEnumerable<InspectorViewModel> Inspectables => Value
        .Cast<object>()
        .Select(inst => new InspectorViewModel() { SelectedObject = Builder(inst) });

    private static InspectableObject DefaultBuilder(object instance)
    {
        return new InspectableObjectBuilder()
            .WithObjectProperties(instance, _ => true)
            .ToInspectableObject();
    }
}

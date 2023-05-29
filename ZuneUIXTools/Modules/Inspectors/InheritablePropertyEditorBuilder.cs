using Gemini.Modules.Inspector.Conventions;
using Gemini.Modules.Inspector.Inspectors;
using System.ComponentModel;

namespace ZuneUIXTools.Modules.Inspectors;

public class InheritablePropertyEditorBuilder<T, TEditor> : PropertyEditorBuilder
    where TEditor : IEditor, new()
{
    public override bool IsApplicable(PropertyDescriptor propertyDescriptor)
    {
        return propertyDescriptor.PropertyType.IsAssignableTo(typeof(T));
    }

    public override IEditor BuildEditor(PropertyDescriptor propertyDescriptor)
    {
        return new TEditor();
    }
}

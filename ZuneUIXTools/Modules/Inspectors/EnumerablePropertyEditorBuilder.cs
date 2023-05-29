using System.Collections;
using System.ComponentModel;

namespace ZuneUIXTools.Modules.Inspectors;

public class EnumerablePropertyEditorBuilder : InheritablePropertyEditorBuilder<IEnumerable, EnumerableEditorViewModel>
{
    public override bool IsApplicable(PropertyDescriptor propertyDescriptor)
    {
        return base.IsApplicable(propertyDescriptor) && propertyDescriptor.PropertyType != typeof(string);
    }
}

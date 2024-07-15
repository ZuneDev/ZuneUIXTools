using Avalonia;

namespace IrisInspector.Themes;

public interface IThemeManager
{
    void Initialize(Application application);

    void Switch(int index);
}

using Gemini.Framework.Menus;
using System.ComponentModel.Composition;

namespace ZuneUIXTools.Modules.Shell;

public static class MenuDefinitions
{
    [Export]
    public static readonly MenuDefinition DebugMenu = new(Gemini.Modules.MainMenu.MenuDefinitions.MainMenuBar, 4, "Debug");

    [Export]
    public static readonly MenuItemGroupDefinition DebuggerMenuGroup = new(DebugMenu, 0);
}

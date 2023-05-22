using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands;

[CommandDefinition]
public class BuildAndDebugCommandDefinition : CommandDefinition
{
    public const string CommandName = "Project.BuildAndDebug";

    public override string Name => CommandName;

    public override string Text => "Start Debugging";

    public override string ToolTip => "Compiles the current UIX document, runs it, and attaches the debugger.";

    public override Uri IconSource => new("pack://application:,,,/ZuneUIXTools;component/Images/Run_16x.png");

    [Export]
    public static CommandKeyboardShortcut KeyGesture
        = new CommandKeyboardShortcut<BuildAndRunCommandDefinition>(new KeyGesture(Key.F5));
}

using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands;

[CommandDefinition]
public class StopDebuggerCommandDefinition : CommandDefinition
{
    public const string CommandName = "Debugger.Detach";

    public override string Name => CommandName;

    public override string Text => "Detach Debugger";

    public override string ToolTip => "Stops the Iris debugger client.";

    public override Uri IconSource => new("pack://application:,,,/ZuneUIXTools;component/Images/Stop_16x.png");

    [Export]
    public static CommandKeyboardShortcut KeyGesture
        = new CommandKeyboardShortcut<StopDebuggerCommandDefinition>(new KeyGesture(Key.F5, ModifierKeys.Shift));
}

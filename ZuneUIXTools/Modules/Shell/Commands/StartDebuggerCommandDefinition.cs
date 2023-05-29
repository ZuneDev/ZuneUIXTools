using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands;

[CommandDefinition]
public class StartDebuggerCommandDefinition : CommandDefinition
{
    public const string CommandName = "Debugger.Attach";

    public override string Name => CommandName;

    public override string Text => "Attach Debugger";

    public override string ToolTip => "Starts the Iris debugger client.";

    public override Uri IconSource => new("pack://application:,,,/ZuneUIXTools;component/Images/EnableDebugging_16x.png");

    [Export]
    public static CommandKeyboardShortcut KeyGesture
        = new CommandKeyboardShortcut<StartDebuggerCommandDefinition>(new KeyGesture(Key.F5, ModifierKeys.Shift));
}

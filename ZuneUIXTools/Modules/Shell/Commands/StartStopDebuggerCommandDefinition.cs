using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands;

[CommandDefinition]
public class StartStopDebuggerCommandDefinition : CommandDefinition
{
    private const string Text_Stop = "Deatch Debugger";
    private const string ToolTip_Stop = "Stops the Iris debugger client.";
    private static readonly Uri IconSource_Stop = new("pack://application:,,,/ZuneUIXTools;component/Images/Stop_16x.png");

    public const string CommandName = "Debugger.ToggleDebugger";

    public override string Name => CommandName;

    public override string Text => Text_Stop;

    public override string ToolTip => ToolTip_Stop;

    public override Uri IconSource => IconSource_Stop;

    [Export]
    public static CommandKeyboardShortcut KeyGesture
        = new CommandKeyboardShortcut<StartStopDebuggerCommandDefinition>(new KeyGesture(Key.F5, ModifierKeys.Shift));

    public static void UpdateCommand(Command command, bool isRunning)
    {
        if (isRunning)
        {
            command.Text = Text_Stop;
            command.ToolTip = ToolTip_Stop;
            command.IconSource = IconSource_Stop;
        }
        else
        {
            command.Text = "Attach Debugger";
            command.ToolTip = "Starts the Iris debugger client.";
            command.IconSource = new("pack://application:,,,/ZuneUIXTools;component/Images/EnableDebugging_16x.png");
        }
    }
}

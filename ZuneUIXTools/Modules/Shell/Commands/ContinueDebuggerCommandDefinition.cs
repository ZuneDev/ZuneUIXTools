using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands;

[CommandDefinition]
public class ContinueDebuggerCommandDefinition : CommandDefinition
{
    private const string Text_Stop = "Continue";
    private const string ToolTip_Stop = "Continues execution until the next breakpoint is hit.";
    private static readonly Uri IconSource_Stop = new("pack://application:,,,/ZuneUIXTools;component/Images/DebugContinue_16x.png");

    public const string CommandName = "Debugger.Continue";

    public override string Name => CommandName;

    public override string Text => Text_Stop;

    public override string ToolTip => ToolTip_Stop;

    public override Uri IconSource => IconSource_Stop;

    [Export]
    public static CommandKeyboardShortcut KeyGesture
        = new CommandKeyboardShortcut<ContinueDebuggerCommandDefinition>(new KeyGesture(Key.F11));
}

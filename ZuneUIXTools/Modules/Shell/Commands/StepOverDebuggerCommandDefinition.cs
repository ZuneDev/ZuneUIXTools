using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands;

[CommandDefinition]
public class StepOverDebuggerCommandDefinition : CommandDefinition
{
    private const string Text_Stop = "Step Over";
    private const string ToolTip_Stop = "Executes the current instruction and breaks at the next.";
    private static readonly Uri IconSource_Stop = new("pack://application:,,,/ZuneUIXTools;component/Images/StepOver_16x.png");

    public const string CommandName = "Debugger.StepOver";

    public override string Name => CommandName;

    public override string Text => Text_Stop;

    public override string ToolTip => ToolTip_Stop;

    public override Uri IconSource => IconSource_Stop;

    [Export]
    public static CommandKeyboardShortcut KeyGesture
        = new CommandKeyboardShortcut<StepOverDebuggerCommandDefinition>(new KeyGesture(Key.F10));
}

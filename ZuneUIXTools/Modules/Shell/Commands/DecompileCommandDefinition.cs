using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands
{
	[CommandDefinition]
	public class DecompileCommandDefinition : CommandDefinition
	{
		public const string CommandName = "Project.Decompile";

		public override string Name => CommandName;

		public override string Text => "Decompile";

		public override string ToolTip => "Attempts to decompile the current UIB document.";

		public override Uri IconSource => new("pack://application:,,,/ZuneUIXTools;component/Images/ZuneShell.ico");

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<DecompileCommandDefinition>(new KeyGesture(Key.F5, ModifierKeys.Shift));
	}
}

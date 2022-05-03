using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace ZuneUIXTools.Modules.Shell.Commands
{
	[CommandDefinition]
	public class BuildAndRunCommandDefinition : CommandDefinition
	{
		public const string CommandName = "Project.BuildAndRun";

		public override string Name => CommandName;

		public override string Text => "Build and Run";

		public override string ToolTip => "Compiles the current UIX document and runs it.";

		public override Uri IconSource => new("pack://application:,,,/ZuneUIXTools;component/Images/StartWithoutDebug_16x.png");

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<BuildAndRunCommandDefinition>(new KeyGesture(Key.F5));
	}
}

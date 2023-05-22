using Gemini.Framework.ToolBars;
using System.ComponentModel.Composition;
using ZuneUIXTools.Modules.Shell.Commands;

namespace ZuneUIXTools.Modules.Shell
{
	internal static class ToolBarDefinitions
	{
		[Export]
		public static ToolBarDefinition UIXToolBar = new(2, "Microsoft Iris UI");

		[Export]
		public static ToolBarItemGroupDefinition UIXFileToolBarGroup = new(UIXToolBar, 8);

		[Export]
		public static ToolBarItemDefinition BuildAndDebugToolBarItem = new CommandToolBarItemDefinition<BuildAndDebugCommandDefinition>(
			UIXFileToolBarGroup, 0, ToolBarItemDisplay.IconAndText);

		[Export]
		public static ToolBarItemDefinition BuildAndRunToolBarItem = new CommandToolBarItemDefinition<BuildAndRunCommandDefinition>(
			UIXFileToolBarGroup, 1);

		[Export]
		public static ToolBarItemDefinition DecompileToolBarItem = new CommandToolBarItemDefinition<DecompileCommandDefinition>(
			UIXFileToolBarGroup, 2);
	}
}

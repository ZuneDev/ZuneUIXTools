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

        [Export]
        public static ToolBarItemGroupDefinition UIXDebuggerToolBarGroup = new(UIXToolBar, 9);

        [Export]
        public static ToolBarItemDefinition StartStopDebuggerToolBarItem = new CommandToolBarItemDefinition<StartStopDebuggerCommandDefinition>(
            UIXDebuggerToolBarGroup, 0, ToolBarItemDisplay.IconOnly);

        [Export]
        public static ToolBarItemDefinition StepOverDebuggerToolBarItem = new CommandToolBarItemDefinition<StepOverDebuggerCommandDefinition>(
            UIXDebuggerToolBarGroup, 4, ToolBarItemDisplay.IconOnly);

        [Export]
        public static ToolBarItemDefinition ContinueDebuggerToolBarItem = new CommandToolBarItemDefinition<ContinueDebuggerCommandDefinition>(
            UIXDebuggerToolBarGroup, 1, ToolBarItemDisplay.IconOnly);
    }
}

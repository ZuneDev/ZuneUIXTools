using Gemini.Framework.ToolBars;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZuneUIXTools.Modules.Shell.Commands;

namespace ZuneUIXTools.Modules.Shell
{
	internal static class ToolBarDefinitions
	{
		[Export]
		public static ToolBarDefinition StandardToolBar = new(0, "Standard");

		[Export]
		public static ToolBarItemGroupDefinition StandardOpenSaveToolBarGroup = new(StandardToolBar, 8);

		[Export]
		public static ToolBarItemDefinition OpenFileToolBarItem = new CommandToolBarItemDefinition<BuildAndRunCommandDefinition>(
			StandardOpenSaveToolBarGroup, 0);
	}
}

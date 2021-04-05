using Microsoft.Iris;
using System.IO;

namespace IrisProjTemplate
{
    class Program
	{
		// The name of the UIX file to load on startup (excluding the file extension)
		// If left blank, Iris will load "Default"
		private const string StartupUIX = "MainPage";

		// The name of the root UI element
		private const string UIRoot = "";

		static void Main(string[] args)
		{
			Application.Initialize();
			Application.Window.SetBackgroundColor(new WindowColor(0xE6, 0xE6, 0xE6));
			Load(StartupUIX, UIRoot);
			Application.Run();

			// Any code after this point is run after the main program exits
		}

		/// <summary>
		/// Requests the specified UIX file to be loaded into the application window
		/// </summary>
		/// <param name="startupUIX">The name of the UIX file to load on startup (excluding the file extension)</param>
		/// <param name="uiRoot">The name of the root UI element</param>
		static void Load(string startupUIX, string uiRoot)
		{
			string curDir = Directory.GetCurrentDirectory();
			string uibPath = Path.Combine(curDir, "Resources", startupUIX + ".uib");
			if (!File.Exists(uibPath))
				throw new FileNotFoundException($"File '{uibPath}' could not be loaded because it does not exist");

			Application.Window.RequestLoad("file://" + uibPath + (string.IsNullOrEmpty(uiRoot) ? string.Empty : "#" + uiRoot));
		}
	}
}

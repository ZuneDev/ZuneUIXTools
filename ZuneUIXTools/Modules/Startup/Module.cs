using Gemini.Framework;
using Gemini.Modules.ErrorList;
using Gemini.Modules.Output;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ZuneUIXTools.Modules.Startup
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        private readonly IOutput _output;
        private readonly IErrorList _errorList;

        [ImportingConstructor]
        public Module(IOutput output, IErrorList errorList)
        {
            _output = output;
            _errorList = errorList;
        }

        public override void Initialize()
        {
            Shell.ShowFloatingWindowsInTaskbar = true;
            Shell.ToolBars.Visible = true;

            //MainWindow.WindowState = WindowState.Maximized;
            MainWindow.Title = "Zune UIX Tools";
            MainWindow.Icon = new BitmapImage(new Uri("pack://application:,,,/ZuneUIXTools;component/Images/VCSIrisProject.ico"));

            MarkupSystem.Startup(true);
            ErrorManager.OnErrors += (IList errors) => {
                foreach (object obj in errors)
                {
                    if (obj is not ErrorRecord err) continue;

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(err.Message);
#else
                    Console.WriteLine(err.Message);
#endif

                    _errorList.AddItem(err.Warning ? ErrorListItemType.Warning : ErrorListItemType.Error,
                        err.Message, err.Context, err.Line < 0 ? null : err.Line, err.Column < 0 ? null : err.Column);
                }
            };

            Shell.StatusBar.AddItem("UIX ready", new GridLength(1, GridUnitType.Star));

            _output.AppendLine("Initialized UIX markup system");
        }
    }
}

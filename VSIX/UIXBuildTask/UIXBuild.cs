using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System;
using System.Linq;
using System.IO;
using Microsoft.Iris.Markup;
using Microsoft.Iris;
using Microsoft.Iris.Session;
using System.Collections;

namespace UIXBuildTask
{
    public class UIXBuild : Task
    {
        private readonly string[] RequiredDlls = new[]
        {
            "UIXrender.dll", "UIX.renderapi.dll", "UIXsup.dll"
        };

        private ITaskItem[] _sourcefiles;
        private string[] _compiledFiles;
        private string _compiledOutputDir;

        /// <summary>
        /// A list of all UIX source files to compile.
        /// </summary>
        [Required]
        public ITaskItem[] SourceFiles
        {
            get => _sourcefiles;
            set => _sourcefiles = value;
        }

        /// <summary>
        /// Directory to output compiled files to.
        /// </summary>
        [Required]
        public string CompiledOutputDir
        {
            get => _compiledOutputDir;
            set => _compiledOutputDir = value;
        }

        /// <summary>
        /// A list of all UIB files generated.
        /// </summary>
        [Output]
        public string[] CompiledFiles
        {
            get => _compiledFiles;
            set => _compiledFiles = value;
        }

        public override bool Execute()
        {
            if (SourceFiles.Length == 0)
                return true;

            // The task is run from the project directory, not the output directory,
            // so the Iris library will fail to find UIXrender.dll unless we move it.
            foreach (string dll in RequiredDlls)
                File.Copy(
                    Path.Combine(Directory.GetCurrentDirectory(), "lib", dll),
                    Path.Combine(Directory.GetCurrentDirectory(), dll),
                    true);

            bool isSuccess = true;
            try
            {
                // Initialize UIX compiler
                MarkupSystem.Startup(true);
                ErrorManager.OnErrors += (IList errors) => {
                    foreach (object obj in errors)
                    {
                        if (obj is ErrorRecord err)
                            Log.LogError("", "", "", err.Context, err.Line, err.Column, err.Line, err.Column, err.Message);
                        else
                            Log.LogError(obj.ToString());
                    }
                };

                foreach (ITaskItem item in SourceFiles)
                {
                    string sourceFile = item.GetMetadata("FullPath");
                    string compiledFile = Path.Combine(CompiledOutputDir, Path.GetFileNameWithoutExtension(sourceFile) + ".uib");
                    Directory.CreateDirectory(CompiledOutputDir);

                    // Compile sourePath
                    isSuccess &= MarkupCompiler.Compile(
                        new[]
                        {
                            new CompilerInput()
                            {
                                SourceFileName = sourceFile,
                                OutputFileName = compiledFile
                            }
                        },
                        new CompilerInput()
                    );

                    if (!isSuccess)
                        Log.LogError($"Failed to compile {sourceFile}");
                }
            }
            finally
            {
                // TODO: Don't leave random DLLs in the project directory
                //foreach (string dll in RequiredDlls)
                //    File.Delete(Path.Combine(Directory.GetCurrentDirectory(), dll));
            }

            return isSuccess;
        }
    }
}

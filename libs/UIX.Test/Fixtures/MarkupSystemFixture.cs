using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using System.Collections;
using Xunit.Abstractions;

namespace UIX.Test.Fixtures;

public class MarkupSystemFixture : IDisposable
{
    private ITestOutputHelper? _output;
    private static bool _handlersInitialized = false;

    public MarkupSystemFixture()
    {
        string zuneDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zune");
        if (Directory.Exists(zuneDirectory))
        {
            // Zune is installed, attempt to copy the required UIX dependencies
            var workingDirectory = Environment.CurrentDirectory;
            IEnumerable<string> irisDependencies = ["UIXRender.dll", "UIXControls.dll"];

            foreach (string irisDependency in irisDependencies)
            {
                var dstFilePath = Path.Combine(workingDirectory, irisDependency);
                if (Path.Exists(dstFilePath))
                    continue;

                var srcFilePath = Path.Combine(zuneDirectory, irisDependency);
                if (!Path.Exists(srcFilePath))
                    continue;

                File.Copy(srcFilePath, dstFilePath);
            }
        }

        MarkupSystem.Startup(true);
        Assembler.RegisterLoader();
    }

    public void SetupDebug(ITestOutputHelper output)
    {
        _output = output;

        if (!_handlersInitialized)
        {
            _handlersInitialized = true;
            ErrorManager.OnErrors += PrintMarkupErrors;
            TraceSettings.Current.OnWriteLine += PrintLine;
        }
    }

    public void Dispose()
    {
        MarkupSystem.Shutdown();
        ErrorManager.OnErrors -= PrintMarkupErrors;
        TraceSettings.Current.OnWriteLine -= PrintLine;
    }

    private void PrintMarkupErrors(IList errors)
    {
        if (_output is null)
            return;

        foreach (ErrorRecord error in errors)
        {
            var errorTypeText = error.Warning ? "Warning" : "Error";
            _output.WriteLine($"{errorTypeText} at (L{error.Line}, C{error.Column}): {error.Message}");
        }
    }

    private void PrintLine(string line) => _output?.WriteLine(line);
}

[CollectionDefinition("MarkupSystem")]
public class MarkupSystemCollection : ICollectionFixture<MarkupSystemFixture>
{
    public const string Name = "MarkupSystem";

    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

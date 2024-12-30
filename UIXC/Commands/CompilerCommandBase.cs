using Errata;
using Microsoft.Iris.Session;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

namespace UIXC.Commands;

public abstract class CompilerCommandBase<TSettings> : Command<TSettings> where TSettings : CompilerSettings
{
    private string[]? _searchPaths;

    public Report? Report { get; private set; }

    protected void BeginErrorReporting(IrisSourceRepository repo)
    {
        Report = new Report(repo);
        ErrorManager.OnErrors += OnError;
    }

    private void OnError(IList errors)
    {
        foreach (ErrorRecord error in errors)
        {
            var l = error.Line;
            var c = error.Column;
            var msg = error.Message;
            var ctx = error.Context;

            var diagnostic = error.Warning
                ? Diagnostic.Warning(msg)
                : Diagnostic.Error(msg);

            if (ctx is not null)
            {
                if (error.Line >= 0 && error.Column >= 0)
                {
                    var label = new Label(ctx, new Location(l, c), "")
                        .WithColor(error.Warning ? Color.Yellow : Color.Red);

                    diagnostic.Labels.Add(label);
                }
                else
                {
                    diagnostic.Note = ctx;
                }
            }

            Report?.AddDiagnostic(diagnostic);
        }
    }

    protected void StopErrorReporting()
    {
        ErrorManager.OnErrors -= OnError;
    }

    protected static string ResolvePath(string givenPath, ICollection<string> searchPaths)
    {
        if (!TryResolvePath(givenPath, searchPaths, out var assemblyPath))
            throw new FileNotFoundException(null, givenPath);
        return assemblyPath;
    }

    protected static bool TryResolvePath(string givenPath, ICollection<string> searchPaths, [NotNullWhen(true)] out string? absolutePath)
    {
        if (Path.IsPathFullyQualified(givenPath))
        {
            absolutePath = givenPath;
            return Path.Exists(absolutePath);
        }

        absolutePath = null;
        if (searchPaths.Count == 0)
            return false;

        foreach (var searchPath in searchPaths)
        {
            absolutePath = Path.GetFullPath(givenPath, searchPath);
            if (Path.Exists(absolutePath))
                return true;
        }

        return false;
    }

    [MemberNotNull(nameof(_searchPaths))]
    protected string[] GetSearchPaths(TSettings settings)
    {
        return _searchPaths ??= settings.IncludeDirectories.Prepend(Environment.CurrentDirectory).ToArray();
    }

    [MemberNotNull(nameof(_searchPaths))]
    protected int LoadAssemblies(TSettings settings)
    {
        GetSearchPaths(settings);

        foreach (var givenPath in settings.Assemblies ?? [])
        {
            try
            {
                var assemblyPath = ResolvePath(givenPath, _searchPaths);
                _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                if (!AnsiConsole.Confirm("Do you want to continue?", false))
                    return -1;
            }
        }

        return 0;
    }
}

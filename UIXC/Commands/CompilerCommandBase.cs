using Errata;
using Microsoft.Iris.Session;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections;

namespace UIXC.Commands;

public abstract class CompilerCommandBase<TSettings> : Command<TSettings> where TSettings : CompilerSettings
{
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
}

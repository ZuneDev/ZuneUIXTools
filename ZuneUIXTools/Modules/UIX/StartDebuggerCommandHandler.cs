using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using ZuneUIXTools.Modules.Shell.Commands;

namespace ZuneUIXTools.Modules.UIX;

[CommandHandler]
public class StartDebuggerCommandHandler : CommandHandlerBase<StartDebuggerCommandDefinition>
{
    private readonly DebuggerService _debuggerService;

    [ImportingConstructor]
    public StartDebuggerCommandHandler(DebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
    }

    public override Task Run(Command command)
    {
        _debuggerService.Start(App.DEFAULT_DEBUG_URI);
        Update(command);

        return Task.CompletedTask;
    }

    public override void Update(Command command)
    {
        base.Update(command);

        command.Enabled = !_debuggerService.IsRunning;
    }
}

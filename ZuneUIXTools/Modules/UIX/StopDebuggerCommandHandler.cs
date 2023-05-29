using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using ZuneUIXTools.Modules.Shell.Commands;

namespace ZuneUIXTools.Modules.UIX;

[CommandHandler]
public class StopDebuggerCommandHandler : CommandHandlerBase<StopDebuggerCommandDefinition>
{
    private readonly DebuggerService _debuggerService;

    [ImportingConstructor]
    public StopDebuggerCommandHandler(DebuggerService debuggerService)
    {
        _debuggerService = debuggerService;
    }

    public override Task Run(Command command)
    {
        _debuggerService.Stop();
        Update(command);

        return Task.CompletedTask;
    }

    public override void Update(Command command)
    {
        base.Update(command);

        command.Visible = command.Enabled = _debuggerService.IsRunning;
    }
}

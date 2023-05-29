using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Modules.Inspector;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using ZuneUIXTools.Modules.Shell.Commands;

namespace ZuneUIXTools.Modules.UIX;

[CommandHandler]
public class StopDebuggerCommandHandler : CommandHandlerBase<StopDebuggerCommandDefinition>
{
    private readonly DebuggerService _debuggerService;
    private readonly IShell _shell;

    [ImportingConstructor]
    public StopDebuggerCommandHandler(DebuggerService debuggerService, IShell shell)
    {
        _debuggerService = debuggerService;
        _shell = shell;
    }

    public override async Task Run(Command command)
    {
        // TODO: Combine Start and Stop commands into one definition and handler
        _debuggerService.Stop();
        Update(command);

        var inspector = IoC.Get<IInspectorTool>();
        await inspector.TryCloseAsync();
    }

    public override void Update(Command command)
    {
        base.Update(command);

        command.Visible = command.Enabled = _debuggerService.IsRunning;
    }
}

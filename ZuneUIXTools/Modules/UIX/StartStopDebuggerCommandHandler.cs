using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Modules.Inspector;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using ZuneUIXTools.Modules.Shell.Commands;
using ZuneUIXTools.Modules.UIXCompiled;

namespace ZuneUIXTools.Modules.UIX;

[CommandHandler]
public class StartStopDebuggerCommandHandler : CommandHandlerBase<StartStopDebuggerCommandDefinition>
{
    private readonly DebuggerService _debuggerService;
    private readonly IShell _shell;
    private readonly IInspectorTool _inspector;
    private UIBDisassemblyViewModel _disassemblyViewModel;

    [ImportingConstructor]
    public StartStopDebuggerCommandHandler(DebuggerService debuggerService, IShell shell, IInspectorTool inspector)
    {
        _debuggerService = debuggerService;
        _shell = shell;
        _inspector = inspector;
    }

    public override async Task Run(Command command)
    {
        if (_debuggerService.IsRunning)
        {
            _debuggerService.Stop();
            await _inspector.TryCloseAsync();
        }
        else
        {
            _debuggerService.Start();
            _disassemblyViewModel = new(_debuggerService, _shell);
            await _shell.OpenDocumentAsync(_disassemblyViewModel);
        }

        Update(command);
    }

    public override void Update(Command command)
    {
        base.Update(command);

        StartStopDebuggerCommandDefinition.UpdateCommand(command, _debuggerService.IsRunning);
    }
}

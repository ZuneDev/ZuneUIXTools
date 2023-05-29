using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Modules.Inspector;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using ZuneUIXTools.Modules.Shell.Commands;
using ZuneUIXTools.Modules.UIXCompiled;

namespace ZuneUIXTools.Modules.UIX;

[CommandHandler]
public class StartDebuggerCommandHandler : CommandHandlerBase<StartDebuggerCommandDefinition>
{
    private readonly DebuggerService _debuggerService;
    private readonly IShell _shell;
    private UIBDisassemblyViewModel _disassemblyViewModel;

    [ImportingConstructor]
    public StartDebuggerCommandHandler(DebuggerService debuggerService, IShell shell)
    {
        _debuggerService = debuggerService;
        _shell = shell;
    }

    public override async Task Run(Command command)
    {
        _debuggerService.Start(App.DEFAULT_DEBUG_URI);
        Update(command);

        _disassemblyViewModel = new(_debuggerService, _shell);
        await _shell.OpenDocumentAsync(_disassemblyViewModel);
    }

    public override void Update(Command command)
    {
        base.Update(command);

        command.Enabled = !_debuggerService.IsRunning;
    }
}

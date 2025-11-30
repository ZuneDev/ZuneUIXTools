using System.Threading;
using System.Threading.Tasks;
using Microsoft.Iris.Debug;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class ContinueHandler : ContinueHandlerBase
{
    private readonly IDebuggerServer _debugService;

    public ContinueHandler(IDebuggerServer debugService) => _debugService = debugService;

    public override Task<ContinueResponse> Handle(ContinueArguments request, CancellationToken cancellationToken)
    {
        _debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Continue;
        return Task.FromResult(new ContinueResponse());
    }
}

internal class NextHandler : NextHandlerBase
{
    private readonly IDebuggerServer _debugService;

    public NextHandler(IDebuggerServer debugService) => _debugService = debugService;

    public override Task<NextResponse> Handle(NextArguments request, CancellationToken cancellationToken)
    {
        _debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Step;
        return Task.FromResult(new NextResponse());
    }
}

internal class PauseHandler : PauseHandlerBase
{
    private readonly IDebuggerServer _debugService;

    public PauseHandler(IDebuggerServer debugService) => _debugService = debugService;

    public override Task<PauseResponse> Handle(PauseArguments request, CancellationToken cancellationToken)
    {
        _debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Break;
        return Task.FromResult(new PauseResponse());
    }
}

internal class StepInHandler : StepInHandlerBase
{
    private readonly IDebuggerServer _debugService;

    public StepInHandler(IDebuggerServer debugService) => _debugService = debugService;

    public override Task<StepInResponse> Handle(StepInArguments request, CancellationToken cancellationToken)
    {
        _debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Step;
        return Task.FromResult(new StepInResponse());
    }
}

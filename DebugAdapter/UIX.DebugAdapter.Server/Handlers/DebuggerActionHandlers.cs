using System.Threading;
using System.Threading.Tasks;
using Microsoft.Iris.Debug;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class ContinueHandler(IDebuggerServer debugService) : ContinueHandlerBase
{
    public override Task<ContinueResponse> Handle(ContinueArguments request, CancellationToken cancellationToken)
    {
        debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Continue;
        return Task.FromResult(new ContinueResponse());
    }
}

internal class NextHandler(IDebuggerServer debugService) : NextHandlerBase
{
    public override Task<NextResponse> Handle(NextArguments request, CancellationToken cancellationToken)
    {
        debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Step;
        return Task.FromResult(new NextResponse());
    }
}

internal class PauseHandler(IDebuggerServer debugService) : PauseHandlerBase
{
    public override Task<PauseResponse> Handle(PauseArguments request, CancellationToken cancellationToken)
    {
        debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Break;
        return Task.FromResult(new PauseResponse());
    }
}

internal class StepInHandler(IDebuggerServer debugService) : StepInHandlerBase
{
    public override Task<StepInResponse> Handle(StepInArguments request, CancellationToken cancellationToken)
    {
        debugService.DebuggerCommand = Debug.Data.InterpreterCommand.Step;
        return Task.FromResult(new StepInResponse());
    }
}

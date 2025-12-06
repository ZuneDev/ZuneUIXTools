using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DapStackFrame = OmniSharp.Extensions.DebugAdapter.Protocol.Models.StackFrame;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class StackTraceHandler : StackTraceHandlerBase
{
    public override async Task<StackTraceResponse> Handle(StackTraceArguments request, CancellationToken cancellationToken)
    {
        Debugger.Launch();
        Debugger.Break();

        List<DapStackFrame> stackFrames = [
            new()
            {
                Source = new()
                {
                    Path = "NowPlayingMusicBackground.uix"
                }
            }
        ];

        return new StackTraceResponse
        {
            StackFrames = stackFrames
        };
    }
}

using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DapThread = OmniSharp.Extensions.DebugAdapter.Protocol.Models.Thread;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class ThreadsHandler : ThreadsHandlerBase
{
    public override Task<ThreadsResponse> Handle(ThreadsArguments request, CancellationToken cancellationToken)
    {
        List<DapThread> threads = [
            new DapThread
            {
                Id = 0,
                Name = "Main Thread"
            }
        ];

        return Task.FromResult(new ThreadsResponse
        {
            Threads = threads
        });
    }
}

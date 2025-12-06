using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class AttachHandler : IAttachHandler, ILaunchHandler
{
    public Task<LaunchResponse> Handle(LaunchRequestArguments request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new LaunchResponse());
    }

    public Task<AttachResponse> Handle(AttachRequestArguments request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AttachResponse());
    }
}

using Microsoft.Iris.Debug.Symbols;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DapBreakpoint = OmniSharp.Extensions.DebugAdapter.Protocol.Models.Breakpoint;
using IrisBreakpoint = Microsoft.Iris.Debug.Data.Breakpoint;

namespace Microsoft.Iris.DebugAdapter.Server.Handlers;

internal class BreakpointHandler(DebugSymbolResolver symbolResolver)
    : ISetInstructionBreakpointsHandler, ISetBreakpointsHandler
{
    public Task<SetInstructionBreakpointsResponse> Handle(SetInstructionBreakpointsArguments request, CancellationToken cancellationToken)
    {
        List<DapBreakpoint> dapBreakpoints = [];

        foreach (var requestedBreakpoint in request.Breakpoints)
        {
            
        }

        SetInstructionBreakpointsResponse response = new()
        {
            Breakpoints = dapBreakpoints
        };
        return Task.FromResult(response);
    }

    public Task<SetBreakpointsResponse> Handle(SetBreakpointsArguments request, CancellationToken cancellationToken)
    {
        // Debug symbols are required for setting source (line:column) breakpoints
        var fsym = symbolResolver?.GetForFile(request.Source.Path);
        if (fsym is null)
            return Task.FromResult(new SetBreakpointsResponse());

        List<DapBreakpoint> dapBreakpoints = [];

        // TODO: Clear existing breakpoints for file

        foreach (var requestedBreakpoint in request.Breakpoints ?? [])
        {
            SourceMap.Entry location;

            if (requestedBreakpoint.Column is not null)
            {
                SourcePosition position = new(requestedBreakpoint.Line, requestedBreakpoint.Column.Value);
                location = fsym.SourceMap.GetLocationFromPosition(position);
            }
            else
            {
                // Get first location that contains this line
                location = fsym.SourceMap.GetLocationFromLine(requestedBreakpoint.Line);
            }

            IrisBreakpoint irisBreakpoint = new(fsym.CompiledFileName, location.Offset);
            Application.DebugSettings.Breakpoints.Add(irisBreakpoint);

            DapBreakpoint dapBreakpoint = new()
            {
                Line = location.Span.Start.Line,
                Column = location.Span.Start.Column,
                EndLine = location.Span.End.Line,
                EndColumn = location.Span.End.Column,
            };
            dapBreakpoints.Add(dapBreakpoint);
        }

        SetBreakpointsResponse response = new()
        {
            Breakpoints = dapBreakpoints
        };
        return Task.FromResult(response);
    }
}

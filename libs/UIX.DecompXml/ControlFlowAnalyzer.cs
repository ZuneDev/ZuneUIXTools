using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Iris.DecompXml;

public static class ControlFlowAnalyzer
{
    private static readonly ImmutableHashSet<OpCode> _jumpOpCodes = [
        OpCode.JumpIfFalse, OpCode.JumpIfFalsePeek, OpCode.JumpIfTruePeek,
        OpCode.JumpIfDictionaryContains, OpCode.JumpIfNullPeek, OpCode.Jump,
    ];

    private static readonly ImmutableHashSet<OpCode> _unconditionalJumpOpCodes = [
        OpCode.ReturnValue, OpCode.ReturnVoid, OpCode.Jump,
    ];

    public static List<ControlFlowBlock> CreateGraph(Instruction[] instructions)
    {
        // See slide 15 (page 8) of https://www.cs.utexas.edu/~lin/cs380c/handout03.pdf

        var procedureMap = instructions.ToImmutableDictionary(i => i.Offset);
        
        // Pass I: Find the start of each block
        HashSet<uint> leaderOffsets = [instructions[0].Offset];

        for (int i = 1; i < instructions.Length; i++)
        {
            var instruction = instructions[i];

            if (!_jumpOpCodes.Contains(instruction.OpCode))
                continue;

            var jumpOffset = (uint)instruction.Operands.First().Value;
            leaderOffsets.Add(jumpOffset);

            if (instruction.OpCode is not OpCode.Jump)
                leaderOffsets.Add(instructions[i + 1].Offset);
        }

        // Pass II: Segment the procedure so a leader starts each block
        List<ControlFlowBlock> blocks = [];

        foreach (var leaderOffset in leaderOffsets)
        {
            List<Instruction> body = [procedureMap[leaderOffset]];

            foreach (var instruction in instructions.SkipWhile(i => i.Offset <= leaderOffset))
            {
                if (leaderOffsets.Contains(instruction.Offset))
                    break;

                body.Add(instruction);
            }

            var lastInstruction = body[^1];

            var jumpOffset = _jumpOpCodes.Contains(lastInstruction.OpCode)
                ? (uint)lastInstruction.Operands.First().Value
                : uint.MaxValue;

            var nextOffset = uint.MaxValue;
            if (!_unconditionalJumpOpCodes.Contains(lastInstruction.OpCode))
            {
                nextOffset = instructions
                    .SkipWhile(i => i.Offset <= lastInstruction.Offset)
                    .FirstOrDefault()?.Offset
                    ?? uint.MaxValue;
            }

            ControlFlowBlock block = new(leaderOffset, lastInstruction.Offset, body, nextOffset, jumpOffset);
            blocks.Add(block);
        }

        // Pass III: Resolve next and branch target blocks
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];

            if (block.NextOffset is not uint.MaxValue)
                block = block with { Next = blocks[IndexOfBlockFromStartOffset(block.NextOffset)] };

            if (block.BranchTargetOffset is not uint.MaxValue)
                block = block with { BranchTarget = blocks[IndexOfBlockFromStartOffset(block.BranchTargetOffset)] };

            blocks[b] = block;
        }

        return blocks;

        int IndexOfBlockFromStartOffset(uint startOffset)
        {
            for (int b = 0; b <= blocks.Count; b++)
            {
                var block = blocks[b];
                if (block.StartOffset == startOffset)
                    return b;
            }

            return -1;
        }
    }

    public static string SerializeToGraphviz(IEnumerable<ControlFlowBlock> blocks)
    {
        var sortedBlocks = blocks.OrderBy(b => b.StartOffset).ToArray();
        StringBuilder sb = new();

        sb.AppendLine("digraph G {");
        sb.AppendLine("    splines=polyline;");
        sb.AppendLine("    node [shape=none];");

        var rank = 0;
        foreach (var block in blocks)
        {
            var nodeId = block.StartOffset;

            sb.AppendLine($"    {nodeId} [label=<");
            sb.AppendLine($"        <table border=\"0\" cellborder=\"1\" cellspacing=\"0\">");

            foreach (var instruction in block.Body)
            {
                sb.AppendLine($"            <tr><td align=\"left\">0x{instruction.Offset:X4}</td><td align=\"left\">{instruction}</td></tr>");
            }
            
            sb.AppendLine($"        </table>");
            sb.AppendLine($"    >];");

            if (block.NextOffset is not uint.MaxValue)
                sb.AppendLine($"    {nodeId} -> {block.NextOffset};");

            if (block.BranchTargetOffset is not uint.MaxValue)
                sb.AppendLine($"    {nodeId} -> {block.BranchTargetOffset} [color=red];");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }
}

public record ControlFlowBlock(uint StartOffset, uint EndOffset, List<Instruction> Body,
    uint NextOffset = uint.MaxValue, uint BranchTargetOffset = uint.MaxValue,
    ControlFlowBlock Next = null, ControlFlowBlock BranchTarget = null);

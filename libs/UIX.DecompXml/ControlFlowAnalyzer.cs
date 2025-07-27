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

            ControlFlowBlock block = new(leaderOffset, body[^1].Offset, body);
            blocks.Add(block);
        }

        // Pass III: Resolve next and branch target blocks
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];

            var leaderInstruction = block.Body[^1];
            var jumpOffset = (uint)leaderInstruction.Operands.First().Value;
            leaderOffsets.Add(jumpOffset);

            if (leaderInstruction.OpCode is not OpCode.Jump)
                leaderOffsets.Add(instructions[i + 1].Offset);
        }

        return blocks;
    }

    public static string SerializeToGraphviz(IEnumerable<ControlFlowBlock> blocks)
    {
        var sortedBlocks = blocks.OrderBy(b => b.StartOffset).ToArray();
        StringBuilder sb = new();

        sb.AppendLine("digraph G {");
        sb.AppendLine("    node [shape=record];");



        sb.AppendLine("}");

        return sb.ToString();
    }
}

public record ControlFlowBlock(uint StartOffset, uint EndOffset, List<Instruction> Body,
    uint NextOffset = uint.MaxValue, uint BranchTargetOffset = uint.MaxValue,
    ControlFlowBlock Next = null, ControlFlowBlock BranchTarget = null);

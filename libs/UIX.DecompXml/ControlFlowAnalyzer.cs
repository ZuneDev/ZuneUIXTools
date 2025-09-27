using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Iris.DecompXml;

public class ControlFlowAnalyzer
{
    private static readonly ImmutableHashSet<OpCode> _jumpOpCodes = [
        OpCode.JumpIfFalse, OpCode.JumpIfFalsePeek, OpCode.JumpIfTruePeek,
        OpCode.JumpIfDictionaryContains, OpCode.JumpIfNullPeek, OpCode.Jump,
    ];

    private static readonly ImmutableHashSet<OpCode> _unconditionalJumpOpCodes = [
        OpCode.ReturnValue, OpCode.ReturnVoid, OpCode.Jump,
    ];

    public ControlFlowAnalyzer(Instruction[] instructions)
    {
        ControlBlocks = CreateGraph(instructions);

        BlockStack = [];
        BlockStack.Push(new(0, instructions[^1].Offset));
    }

    public List<IProgramBlock> ControlBlocks { get; }

    public Stack<CodeBlock> BlockStack { get; }

    public IProgramBlock GetByOffset(uint offset)
    {
        return ControlBlocks.First(b => offset >= b.StartOffset && offset <= b.EndOffset);
    }

    public IProgramBlock GetByStartOffset(uint offset) => ControlBlocks.FirstOrDefault(b => offset == b.StartOffset);

    public IProgramBlock GetByInstruction(Instruction instruction) => GetByOffset(instruction.Offset);

    public void FinalizeCompletedBlocks(uint currentOffset)
    {
        while (BlockStack.Count > 1)
        {
            var currentBlock = BlockStack.Pop();

            if (currentBlock.EndOffset > currentOffset)
            {
                BlockStack.Push(currentBlock);
                break;
            }

            currentBlock.FinalizeBlock(BlockStack.Peek());
        }
    }

    public void PushBlock(CodeBlock block) => BlockStack.Push(block);

    public bool TryPeekBlockInfo<T>(out T info) where T : ICodeBlockInfo => TryPeekBlock(out _, out info);

    public bool TryPeekBlock<T>(out CodeBlock block, out T info) where T : ICodeBlockInfo
    {
        if (BlockStack.Count > 1)
        {
            var currentBlock = BlockStack.Peek();
            if (currentBlock.Info is T a)
            {
                block = currentBlock;
                info = a;
                return true;
            }
        }

        block = default;
        info = default;
        return false;
    }

    public void AppendToBlock(StatementSyntax statement) => BlockStack.Peek().Statements.Add(statement);

    public bool TryGetLastStatement(out StatementSyntax statement)
    {
        var statements = BlockStack.Peek().Statements;
        if (statements.Count > 0)
        {
            statement = statements[^1];
            return true;
        }

        statement = null;
        return false;
    }

    private static List<IProgramBlock> CreateGraph(Instruction[] instructions)
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
        List<IProgramBlock> blocks = [];

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

            BasicControlFlowBlock block = new(leaderOffset, lastInstruction.Offset, body, nextOffset, jumpOffset);
            blocks.Add(block);
        }

        return blocks;
    }

    public string SerializeToGraphviz()
    {
        var sortedBlocks = ControlBlocks.OrderBy(b => b.StartOffset).ToArray();
        StringBuilder sb = new();

        sb.AppendLine("digraph G {");
        sb.AppendLine("    splines=polyline;");
        sb.AppendLine("    node [shape=none];");

        var rank = 0;
        foreach (var block in sortedBlocks)
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
                sb.AppendLine($"    {nodeId}:s -> {block.NextOffset}:n;");

            if (block is BasicControlFlowBlock { BranchTargetOffset: not uint.MaxValue } cfBlock)
                sb.AppendLine($"    {nodeId}:s -> {cfBlock.BranchTargetOffset}:n [color=red];");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }
}

public interface IProgramBlock
{
    uint StartOffset { get; }
    uint EndOffset { get; }
    List<Instruction> Body { get; }
    uint NextOffset { get; }

    IEnumerable<uint> GetChildrenStartOffsets();
}

public abstract record ProgramBlock(uint StartOffset, uint EndOffset, List<Instruction> Body, uint NextOffset = uint.MaxValue)
    : IProgramBlock
{
    public virtual IEnumerable<uint> GetChildrenStartOffsets()
    {
        if (NextOffset is not uint.MaxValue)
            yield return NextOffset;
    }
}

public record BasicControlFlowBlock(uint StartOffset, uint EndOffset, List<Instruction> Body,
    uint NextOffset = uint.MaxValue, uint BranchTargetOffset = uint.MaxValue)
    : ProgramBlock(StartOffset, EndOffset, Body, NextOffset)
{
    public override IEnumerable<uint> GetChildrenStartOffsets()
    {
        foreach (var offset in base.GetChildrenStartOffsets())
            yield return offset;

        if (BranchTargetOffset is not uint.MaxValue)
            yield return BranchTargetOffset;
    }
}

public static class IProgramBlockExtensions
{
    public static bool HasEdgeTo(this IProgramBlock block, IProgramBlock targetBlock, List<IProgramBlock> blocks)
    {
        return block
            .GetChildren(blocks)
            .Contains(targetBlock);
    }

    public static IEnumerable<IProgramBlock> GetChildren(this IProgramBlock block, List<IProgramBlock> blocks)
    {
        foreach (var startOffset in block.GetChildrenStartOffsets())
            yield return blocks.First(b => b.StartOffset == startOffset);
    }
}

using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml;

public record CodeBlock
{
    public CodeBlock(uint startOffset, uint endOffset, ICodeBlockInfo meta = null)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
        Info = meta;
    }

    public uint StartOffset { get; init; }

    public uint EndOffset { get; init; }

    public ICodeBlockInfo Info { get; init; }

    public List<StatementSyntax> Statements { get; } = [];

    public void FinalizeBlock(CodeBlock parentBlock)
    {
        var blockBody = Block(Statements);

        switch (Info)
        {
            case IfBlockInfo ifBlockInfo:
                var ifStatement = IfStatement(ifBlockInfo.Condition, blockBody);

                if (ifBlockInfo.ElseClause is not null)
                    ifStatement = ifStatement.WithElse(ifBlockInfo.ElseClause);

                parentBlock.Statements.Add(ifStatement);
                break;

            case ElseBlockInfo _:
                if (parentBlock.Info is not IfBlockInfo ifElseBlockInfo)
                    throw new InvalidOperationException($"Else block must be preceded by an if block, got '{parentBlock.Info}'");

                ifElseBlockInfo.ElseClause = ElseClause(blockBody);
                break;

            case ForEachBlockInfo forEachBlockInfo:
                var foreachStatement = ForEachStatement(forEachBlockInfo.Type, forEachBlockInfo.Identifier,
                    forEachBlockInfo.Source, blockBody);
                parentBlock.Statements.Add(foreachStatement);
                break;

            default:
                throw new NotImplementedException($"Unrecognized code block kind '{Info.GetType().Name}'");
        }
    }
}

public interface ICodeBlockInfo;

public class IfBlockInfo(ExpressionSyntax condition = null) : ICodeBlockInfo
{
    public ExpressionSyntax Condition { get; set; } = condition;

    public ElseClauseSyntax ElseClause { get; set; }
}

public class ElseBlockInfo : ICodeBlockInfo;

public class ForEachBlockInfo : ICodeBlockInfo
{
    public ExpressionSyntax Source { get; set; }

    public string Identifier { get; set; }

    public TypeSyntax Type { get; set; }
}

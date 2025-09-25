using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml;

internal record CodeBlockInfo
{
    public CodeBlockInfo(uint startOffset, uint endOffset, ICodeBlockAdditionalInfo additionalInfo = null)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
        AdditionalInfo = additionalInfo;
    }

    public uint StartOffset { get; init; }

    public uint EndOffset { get; init; }

    public ICodeBlockAdditionalInfo AdditionalInfo { get; init; }

    public List<StatementSyntax> Statements { get; } = [];

    public void FinalizeBlock(CodeBlockInfo parentBlock)
    {
        var blockBody = Block(Statements);

        switch (AdditionalInfo)
        {
            case IfBlockInfo ifBlockInfo:
                var ifStatement = IfStatement(ifBlockInfo.Condition, blockBody);
                parentBlock.Statements.Add(ifStatement);
                break;

            case ElseBlockInfo _:
                var elseClause = ElseClause(blockBody);

                var parentLastStatement = parentBlock.Statements[^1];
                if (parentLastStatement is not IfStatementSyntax ifElseBlock)
                    throw new InvalidOperationException($"Else block must be preceded by an if block, got '{parentLastStatement.Kind()}'");

                parentBlock.Statements[^1] = ifElseBlock.WithElse(elseClause);
                break;

            case ForEachBlockInfo forEachBlockInfo:
                var foreachStatement = ForEachStatement(forEachBlockInfo.Type, forEachBlockInfo.Identifier,
                    forEachBlockInfo.Source, blockBody);
                parentBlock.Statements.Add(foreachStatement);
                break;

            default:
                throw new System.NotImplementedException($"Unrecognized code block kind '{AdditionalInfo.GetType().Name}'");
        }
    }
}

internal interface ICodeBlockAdditionalInfo;

internal class IfBlockInfo(ExpressionSyntax condition = null) : ICodeBlockAdditionalInfo
{
    public ExpressionSyntax Condition { get; set; } = condition;
}

internal class ElseBlockInfo : ICodeBlockAdditionalInfo;

internal class ForEachBlockInfo : ICodeBlockAdditionalInfo
{
    public ExpressionSyntax Source { get; set; }

    public string Identifier { get; set; }

    public TypeSyntax Type { get; set; }
}

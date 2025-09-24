using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml;

internal record CodeBlockInfo
{
    public CodeBlockInfo(uint startOffset, uint endOffset, SyntaxKind kind, ExpressionSyntax expression = null)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
        Kind = kind;
        Expression = expression;
    }

    public uint StartOffset { get; init; }

    public uint EndOffset { get; init; }

    public SyntaxKind Kind { get; init; }

    public ExpressionSyntax Expression { get; init; }

    public List<StatementSyntax> Statements { get; } = [];

    public void FinalizeBlock(CodeBlockInfo parentBlock)
    {
        var blockBody = Block(Statements);

        switch (Kind)
        {
            case SyntaxKind.IfStatement:
                var ifStatement = IfStatement(Expression, blockBody);
                parentBlock.Statements.Add(ifStatement);
                break;

            case SyntaxKind.ElseClause:
                var elseClause = ElseClause(blockBody);
                var ifElseBlock = (IfStatementSyntax)parentBlock.Statements[^1];
                parentBlock.Statements[^1] = ifElseBlock.WithElse(elseClause);
                break;

            default:
                throw new System.NotImplementedException($"Unrecognized code block kind '{Kind}'");
        }
    }
}

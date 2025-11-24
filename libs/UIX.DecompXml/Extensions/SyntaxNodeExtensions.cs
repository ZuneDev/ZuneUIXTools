using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Iris.DecompXml.Extensions;

internal static class SyntaxNodeExtensions
{
    public static bool IsStatementExpression(this SyntaxNode syntax)
    {
        // The grammar gives:
        //
        // expression-statement:
        //     statement-expression ;
        //
        // statement-expression:
        //     invocation-expression
        //     object-creation-expression
        //     assignment
        //     post-increment-expression
        //     post-decrement-expression
        //     pre-increment-expression
        //     pre-decrement-expression
        //     await-expression

        switch (syntax.Kind())
        {
            case SyntaxKind.InvocationExpression:
            case SyntaxKind.ObjectCreationExpression:
            case SyntaxKind.SimpleAssignmentExpression:
            case SyntaxKind.AddAssignmentExpression:
            case SyntaxKind.SubtractAssignmentExpression:
            case SyntaxKind.MultiplyAssignmentExpression:
            case SyntaxKind.DivideAssignmentExpression:
            case SyntaxKind.ModuloAssignmentExpression:
            case SyntaxKind.AndAssignmentExpression:
            case SyntaxKind.OrAssignmentExpression:
            case SyntaxKind.ExclusiveOrAssignmentExpression:
            case SyntaxKind.LeftShiftAssignmentExpression:
            case SyntaxKind.RightShiftAssignmentExpression:
            case SyntaxKind.UnsignedRightShiftAssignmentExpression:
            case SyntaxKind.CoalesceAssignmentExpression:
            case SyntaxKind.PostIncrementExpression:
            case SyntaxKind.PostDecrementExpression:
            case SyntaxKind.PreIncrementExpression:
            case SyntaxKind.PreDecrementExpression:
            case SyntaxKind.AwaitExpression:
                return true;

            case SyntaxKind.ConditionalAccessExpression:
                var access = (ConditionalAccessExpressionSyntax)syntax;
                return IsStatementExpression(access.WhenNotNull);

            // Allow missing IdentifierNames; they will show up in error cases
            // where there is no statement whatsoever.

            case SyntaxKind.IdentifierName:
                return syntax.IsMissing;

            default:
                return false;
        }
    }
}

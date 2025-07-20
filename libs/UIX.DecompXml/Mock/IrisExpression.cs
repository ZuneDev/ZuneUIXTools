using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Markup;
using System;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml.Mock;

internal static class IrisExpression
{
    public static ExpressionSyntax ToSyntax(object obj, DecompileContext context)
    {
        return obj switch
        {
            null => LiteralExpression(SyntaxKind.NullLiteralExpression),
            int intValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(intValue)),
            bool boolValue => LiteralExpression(boolValue ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            string strValue => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(strValue)),
            IStringEncodable strEnc => ParseExpression(strEnc.EncodeString()),

            Disassembler.RawConstantInfo constantInfo => ToSyntax(constantInfo.Value, constantInfo.Type, context),
            IrisObject irisObj => ToSyntax(irisObj.Object, irisObj.Type, context),
            SymbolReference symbolRef => ToSyntax(symbolRef),
            TypeSchema typeSchema => ToSyntax(typeSchema, context),

            ExpressionSyntax exprSyn => exprSyn,

            _ => IdentifierName(obj.ToString())
        };
    }

    public static IdentifierNameSyntax ToSyntax(TypeSchema type, DecompileContext context) =>
        IdentifierName(context.GetQualifiedName(type).ToString());

    public static IdentifierNameSyntax ToSyntax(SymbolReference symbolRef) =>
        IdentifierName(symbolRef.Symbol);

    public static ExpressionSyntax ToSyntax(object obj, TypeSchema type, DecompileContext context)
    {
        return obj switch
        {
            Enum _ => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(context.GetQualifiedName(type).ToString()),
                IdentifierName(obj.ToString())
            ),

            _ => ToSyntax(obj, context),
        };
    }
}

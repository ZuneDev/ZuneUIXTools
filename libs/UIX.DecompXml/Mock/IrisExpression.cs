using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Markup;
using System;
using System.Linq.Expressions;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml.Mock;

internal abstract class IrisExpression : Expression
{
    public virtual string Decompile(DecompileContext context) => ToSyntax(context).ToString();

    public abstract ExpressionSyntax ToSyntax(DecompileContext context);

    public static Expression Wrap(object p)
    {
        return p switch
        {
            null => Constant(null),
            Expression expr => expr,
            Disassembler.RawConstantInfo constantInfo => new IrisConstantExpression(constantInfo.Value, constantInfo.Type),
            SymbolReference symbolRef => Constant(symbolRef),

            _ => throw new NotImplementedException($"Unable to wrap '{p}' in an expression")
        };
    }

    public static string Decompile(Expression expr, DecompileContext context)
    {
        return expr is IrisExpression irisExpr
            ? irisExpr.Decompile(context)
            : expr.ToString();
    }

    public static ExpressionSyntax ToSyntax(object obj, DecompileContext context)
    {
        return obj switch
        {
            null => LiteralExpression(SyntaxKind.NullLiteralExpression),
            int intValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(intValue)),
            bool boolValue => LiteralExpression(boolValue ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            string strValue => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(strValue)),
            IStringEncodable strEnc => ParseExpression(strEnc.EncodeString()),

            Disassembler.RawConstantInfo constantInfo => new IrisConstantExpression(constantInfo.Value, constantInfo.Type).ToSyntax(context),
            SymbolReference symbolRef => ToSyntax(symbolRef),
            TypeSchema typeSchema => ToSyntax(typeSchema, context),

            IrisExpression irisExpr => irisExpr.ToSyntax(context),
            Expression expr => ParseExpression(expr.ToString()),
            ExpressionSyntax exprSyn => exprSyn,

            _ => IdentifierName(obj.ToString())
        };
    }

    public static IdentifierNameSyntax ToSyntax(TypeSchema type, DecompileContext context) =>
        IdentifierName(context.GetQualifiedName(type).ToString());

    public static IdentifierNameSyntax ToSyntax(SymbolReference symbolRef) =>
        IdentifierName(symbolRef.Symbol);
}

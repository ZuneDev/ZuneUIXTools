using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml.Mock;

internal static class IrisExpression
{
    private static Dictionary<ulong, SyntaxKind> _predefinedTypeMap = null;

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

    public static TypeSyntax ToSyntax(TypeSchema type, DecompileContext context)
    {
        if (TryMapPredefinedType(type, out var kind))
            return PredefinedType(Token(kind));

        return IdentifierName(context.GetQualifiedName(type).ToString());
    }

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

    private static bool TryMapPredefinedType(TypeSchema type, out SyntaxKind kind)
    {
        _predefinedTypeMap ??= new()
        {
            [UIXTypes.MapIDToType(UIXTypeID.Void).UniqueId] = SyntaxKind.VoidKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Int32).UniqueId] = SyntaxKind.IntKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Int64).UniqueId] = SyntaxKind.LongKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.String).UniqueId] = SyntaxKind.StringKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Boolean).UniqueId] = SyntaxKind.BoolKeyword,
        };

        return _predefinedTypeMap.TryGetValue(type.UniqueId, out kind);
    }
}

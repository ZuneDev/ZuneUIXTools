using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml.Mock;

internal class IrisExpression
{
    private static Dictionary<ulong, SyntaxKind> _predefinedTypeMap = null;

    public static ExpressionSyntax ToSyntax(object obj, uint offset, DecompileContext context)
    {
        var expr = obj switch
        {
            null => LiteralExpression(SyntaxKind.NullLiteralExpression),
            int intValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(intValue)),
            bool boolValue => LiteralExpression(boolValue ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            string strValue => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(strValue)),
            IStringEncodable strEnc => ParseExpression(strEnc.EncodeString()),

            Disassembler.RawConstantInfo constantInfo => ToSyntax(constantInfo.Value, constantInfo.Type, offset, context),
            IrisObject irisObj => ToSyntax(irisObj.Object, irisObj.Type, offset, context),
            SymbolReference symbolRef => ToSyntax(symbolRef),
            TypeSchema typeSchema => ToSyntax(typeSchema, context),

            ExpressionSyntax exprSyn => exprSyn,

            _ => IdentifierName(obj.ToString())
        };

        return expr.WithOffset(offset);
    }

    public static TypeSyntax ToSyntax(TypeSchema type, DecompileContext context)
    {
        if (TryMapPredefinedType(type, out var kind))
            return PredefinedType(Token(kind));

        var typeName = context.GetQualifiedName(type);
        
        if (type.Name.StartsWith("SingletonModelItem"))
        {
            // Special handling for this weird wrapper type
            var genericName = ParseGenericType(typeName);
            var innerFullTypeName = genericName.TypeArgumentList.Arguments[0].ToString();

            // Resolve namespace to prefix
            var nsEndIndex = innerFullTypeName.LastIndexOf('.');
            var innerTypeName = innerFullTypeName[(nsEndIndex + 1)..];
            var ns = innerFullTypeName[..nsEndIndex];
            
            var innerType = context.ImportTables.TypeImports
                .Where(t => t.Name == innerTypeName || t.AlternateName == innerTypeName)
                .FirstOrDefault(t => (t.Owner as AssemblyLoadResult)?.Namespace == ns);

            typeName = context.GetQualifiedName(innerType);
        }

        return IdentifierName(typeName.ToString());
    }

    public static IdentifierNameSyntax ToSyntax(SymbolReference symbolRef) => IdentifierName(symbolRef.Symbol);

    public static ExpressionSyntax ToSyntax(object obj, TypeSchema type, uint offset, DecompileContext context)
    {
        return obj switch
        {
            Enum _ => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(context.GetQualifiedName(type).ToString()),
                IdentifierName(obj.ToString())
            ).WithOffset(offset),

            _ => ToSyntax(obj, offset, context),
        };
    }

    private static bool TryMapPredefinedType(TypeSchema type, out SyntaxKind kind)
    {
        _predefinedTypeMap ??= new()
        {
            [UIXTypes.MapIDToType(UIXTypeID.Void).UniqueId] = SyntaxKind.VoidKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Object).UniqueId] = SyntaxKind.ObjectKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Boolean).UniqueId] = SyntaxKind.BoolKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Byte).UniqueId] = SyntaxKind.ByteKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Int32).UniqueId] = SyntaxKind.IntKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Int64).UniqueId] = SyntaxKind.LongKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Single).UniqueId] = SyntaxKind.FloatKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Double).UniqueId] = SyntaxKind.DoubleKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.Char).UniqueId] = SyntaxKind.CharKeyword,
            [UIXTypes.MapIDToType(UIXTypeID.String).UniqueId] = SyntaxKind.StringKeyword,
        };

        if (_predefinedTypeMap.TryGetValue(type.UniqueId, out kind))
            return true;

        if (type.Equivalents is null)
            return false;

        foreach (var equivalentType in type.Equivalents)
        {
            if (_predefinedTypeMap.TryGetValue(equivalentType.UniqueId, out kind))
                return true;
        }

        return false;
    }

    private static GenericNameSyntax ParseGenericType(QualifiedTypeName typeName)
    {
        Regex rxGeneric = new(@"([A-z_:]\w*)`(\d+)\[(.+)\]");
        var genericMatch = rxGeneric.Match(typeName.TypeName);
        if (!genericMatch.Success)
            throw new ArgumentException("Invalid generic");

        var unqualifiedTypeName = genericMatch.Groups[1].Value;
        var typeArgCount = int.Parse(genericMatch.Groups[2].Value);
        var typeArgNames = genericMatch.Groups[3].Value
            .Split(',')
            .Select(IdentifierName);

        // TODO: Handle nested generics
        var trimmedTypeName = $"{typeName.NamespacePrefix}:{unqualifiedTypeName}";
        return GenericName(Identifier(trimmedTypeName), TypeArgumentList([.. typeArgNames]));
    }
}

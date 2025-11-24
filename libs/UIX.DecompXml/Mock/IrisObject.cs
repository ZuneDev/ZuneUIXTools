using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.DecompXml.Mock;

internal record IrisObject(object Object, TypeSchema Type)
{
    public static IrisObject Create(object objIn, TypeSchema type, DecompileContext context, MarkupTypeSchema parentSchema = null)
    {
        object obj = objIn;

        if (objIn is IrisObject irisObj)
        {
            return irisObj.Type is null
                ? (irisObj with { Type = type })
                : irisObj;
        }
        else if (objIn is null)
        {
            type ??= UIXTypes.MapIDToType(UIXTypeID.Null);
        }
        else if (objIn is ExpressionSyntax expr)
        {
            type ??= GuessExpressionReturnType(expr, context);
            return new(obj, type);
        }
        else if (objIn is SymbolReference symRef && type is null && parentSchema is not null)
        {
            if (symRef.Origin is SymbolOrigin.Properties or SymbolOrigin.Locals)
                type = parentSchema.InheritableSymbolsTable.First(sr => sr.Name == symRef.Symbol).Type;
        }

        if (objIn is Disassembler.RawConstantInfo constantInfo)
        {
            obj = constantInfo.Value;
            type ??= constantInfo.Type;
        }

        type ??= Disassembler.TryGuessTypeSchema(obj.GetType(), context.LoadResult);

        return new(obj, type);
    }

    private static TypeSchema GuessExpressionReturnType(ExpressionSyntax expr, DecompileContext ctx)
    {
        if (expr is ParenthesizedExpressionSyntax parenExpr)
            expr = parenExpr.Expression;

        if (expr is InvocationExpressionSyntax or MemberAccessExpressionSyntax)
        {
            // TODO: Handle invocation of instance methods
            try
            {
                ExpressionSyntax sourceExpr = expr;

                Stack<ArgumentListSyntax> argumentLists = [];
                List<NameSyntax> names = [];

                while (sourceExpr is not NameSyntax)
                {
                    if (sourceExpr is MemberAccessExpressionSyntax innerSourceExpr)
                    {
                        names.Add(innerSourceExpr.Name);
                        sourceExpr = innerSourceExpr.Expression;
                    }
                    else if (sourceExpr is InvocationExpressionSyntax innerInvocationExpr)
                    {
                        argumentLists.Push(innerInvocationExpr.ArgumentList);
                        sourceExpr = innerInvocationExpr.Expression;
                    }
                }

                if (sourceExpr is not NameSyntax sourceName)
                    return null;

                names.Reverse();

                var sourceTypeName = QualifiedTypeName.Parse(sourceName.ToString());
                var sourceType = ctx.GetImportedType(sourceTypeName);

                foreach (var name in names)
                {
                    var memberName = name.ToString();

                    var prop = sourceType.FindPropertyDeep(memberName);
                    if (prop is not null)
                    {
                        sourceType = prop.PropertyType;
                        continue;
                    }

                    var argumentList = argumentLists.Pop();

                    // Try to disambiguate overloads using number of parameters
                    var parameterCount = argumentList.Arguments.Count;

                    var method = sourceType.Methods.FirstOrDefault(
                        m => m.Name == memberName && m.ParameterTypes.Length == parameterCount);

                    sourceType = method.ReturnType;
                }

                return sourceType;
            }
            catch { }
        }
        else if (expr is CastExpressionSyntax castExpr)
        {
            var castTargetName = QualifiedTypeName.Parse(castExpr.Type.ToString());
            return ctx.GetImportedType(castTargetName);
        }
        else if (expr is LiteralExpressionSyntax literalExpr)
        {
            return literalExpr.Kind() switch
            {
                // FIXME: This should be more specific. Detect the best numeric type.
                SyntaxKind.NumericLiteralExpression => UIXTypes.MapIDToType(UIXTypeID.Int32),
                SyntaxKind.Utf8StringLiteralExpression or 
                SyntaxKind.StringLiteralExpression => UIXTypes.MapIDToType(UIXTypeID.String),
                SyntaxKind.CharacterLiteralExpression => UIXTypes.MapIDToType(UIXTypeID.Char),
                SyntaxKind.TrueLiteralExpression or
                SyntaxKind.FalseLiteralExpression => UIXTypes.MapIDToType(UIXTypeID.Boolean),
                SyntaxKind.NullLiteralExpression => UIXTypes.MapIDToType(UIXTypeID.Null),

                SyntaxKind.ArgListExpression or
                SyntaxKind.DefaultLiteralExpression or
                _ => throw new System.NotImplementedException(),
            };
        }

        return null;
    }
}

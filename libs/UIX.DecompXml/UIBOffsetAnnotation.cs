using Microsoft.CodeAnalysis;
using Microsoft.Iris.Asm.Models;

namespace Microsoft.Iris.DecompXml;

internal static class UIBOffsetAnnotation
{
    public const string Kind = "Iris_UIB_Offset";

    public static TNode WithOffset<TNode>(this TNode node, uint offset)
        where TNode : SyntaxNode
    {
        if (node.HasAnnotations(Kind))
            return node;

        return node.WithAdditionalAnnotations(new SyntaxAnnotation(Kind, offset.ToString("R")));
    }

    public static TNode WithOffset<TNode>(this TNode node, Instruction instruction)
        where TNode : SyntaxNode
    {
        return node.WithOffset(instruction.Offset);
    }

    public static uint GetOffset(SyntaxAnnotation offsetAnnotation) => uint.Parse(offsetAnnotation.Data);
}


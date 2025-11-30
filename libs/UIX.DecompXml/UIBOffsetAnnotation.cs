using Microsoft.CodeAnalysis;
using Microsoft.Iris.Asm.Models;
using System.Linq;
using System.Xml.Linq;

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

    public static uint? GetOffset(this SyntaxNode node)
    {
        var annotation = node.GetAnnotations(Kind).SingleOrDefault();
        return annotation is null
            ? null
            : uint.Parse(annotation.Data);
    }

    public static void SetOffset(this XObject xObj, uint offset) => xObj.AddAnnotation(new UIBOffsetXmlAnnotation(offset));

    public static void SetOffset(this XObject xObj, Instruction instruction) => xObj.SetOffset(instruction.Offset);

    public static XObject WithOffset(this XObject xObj, Instruction instruction)
    {
        xObj.SetOffset(instruction.Offset);
        return xObj;
    }

    public static XObject WithNewOffset(this XObject xObj, Instruction instruction)
    {
        if (xObj.GetOffset() is null)
            xObj.SetOffset(instruction.Offset);
        return xObj;
    }

    public static uint? GetOffset(this XObject xObj) => xObj.Annotation<UIBOffsetXmlAnnotation>()?.Offset;
}

internal record UIBOffsetXmlAnnotation(uint Offset);


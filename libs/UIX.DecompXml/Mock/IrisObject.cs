using Microsoft.Iris.Asm;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml.Mock;

internal record IrisObject(object Object, TypeSchema Type)
{
    public static IrisObject Create(object objIn, TypeSchema type, Decompiler decompiler)
    {
        object obj = objIn;

        if (objIn is IrisObject irisObj)
        {
            return irisObj.Type is null
                ? (irisObj with { Type = type })
                : irisObj;
        }

        if (objIn is Disassembler.RawConstantInfo constantInfo)
        {
            obj = constantInfo.Value;
            type ??= constantInfo.Type;
        }

        if (objIn is IReturnValueProvider hasReturnValue)
        {
            type ??= hasReturnValue.ReturnType;
        }

        type ??= Disassembler.GuessTypeSchema(obj.GetType(), decompiler._loadResult);

        return new(obj, type);
    }
}

using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.Asm;

public class ObjectSection
{
    readonly IEnumerable<Instruction> _instructions;
    readonly MarkupLoadResult _loadResult;

    public ObjectSection(IEnumerable<Instruction> instructions, MarkupLoadResult loadResult)
    {
        _instructions = instructions;
        _loadResult = loadResult;
    }

    public ObjectSection(IEnumerable<IBodyItem> body, MarkupLoadResult loadResult)
        : this(body.OfType<Instruction>(), loadResult)
    {
    }

    public ObjectSection(Program program, MarkupLoadResult loadResult)
        : this(program.Body, loadResult)
    {
    }

    public ByteCodeReader Encode()
    {
        ByteCodeWriter writer = new();

        foreach (var instruction in _instructions)
        {
            var opCode = instruction.OpCode;
            writer.WriteByte(opCode);

            foreach (var operand in instruction.Operands)
            {
                switch (operand.Value)
                {
                    case OperationType opType:
                        writer.WriteByte((byte)opType);
                        break;
                    case byte b:
                        writer.WriteByte(b);
                        break;
                    case ushort uint16:
                        writer.WriteUInt16(uint16);
                        break;
                    case uint uint32:
                        writer.WriteUInt32(uint32);
                        break;
                    case int int32:
                        writer.WriteInt32(int32);
                        break;

                    default:
                        if (opCode == OpCode.ConstructFromBinary)
                        {
                            ushort cbinTypeIndex = (UInt16)instruction.Operands.First().Value;
                            TypeSchema cbinTypeSchema = _loadResult.ImportTables.TypeImports[cbinTypeIndex];
                            cbinTypeSchema.EncodeBinary(writer, operand.Value);
                            break;
                        }

                        throw new InvalidOperationException($"Unexpected operand '{operand.Value}' of type '{operand.Value.GetType()}'");
                }
            }
        }

        return writer.CreateReader();
    }
}

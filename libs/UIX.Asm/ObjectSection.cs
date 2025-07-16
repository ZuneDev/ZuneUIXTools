using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.Asm;

public class ObjectSection
{
    readonly Program _program;
    readonly MarkupLoadResult _loadResult;
    Dictionary<string, uint> _labelOffsetMap;

    public ObjectSection(Program program, MarkupLoadResult loadResult)
    {
        _program = program;
        _loadResult = loadResult;
    }

    public IReadOnlyDictionary<string, uint> LabelOffsetMap => _labelOffsetMap;

    public IReadOnlyDictionary<string, ushort> Constants { get; set; }

    public ByteCodeReader Encode()
    {
        ByteCodeWriter writer = new();
        _labelOffsetMap = new();

        foreach (var bodyItem in _program.Code)
        {
            var offset = writer.DataSize;

            if (bodyItem is Label label)
            {
                _labelOffsetMap[label.Name] = offset;
                continue;
            }

            if (bodyItem is not Instruction instruction)
                continue;

            // Add entry to line number table
            _loadResult.LineNumberTable.AddRecord(offset, instruction.Line, instruction.Column);
            _loadResult.LineNumberTable.AddRecord(offset, instruction.Line, 0);

            var opCode = instruction.OpCode;
            writer.WriteByte(opCode);

            foreach (var operand in instruction.Operands)
            {
                object operandValue = operand.Value;

                if (operand is OperandReference operandRef)
                    operandValue = Constants[operandRef.ConstantName];

                switch (operandValue)
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

    public static IEnumerable<ICodeItem> Decode(ByteCodeReader reader, Dictionary<uint, List<Label>> offsetLabelMap = null)
    {
        while (reader.CurrentOffset < reader.Size)
        {
            var offset = reader.CurrentOffset;
            if (offsetLabelMap?.TryGetValue(offset, out var labels) ?? false)
                foreach (var label in labels)
                    yield return label;

            var opCode = (OpCode)reader.ReadByte();

            var instSchema = InstructionSet.InstructionSchema[opCode];
            Operand[] operands = new Operand[instSchema.Length];

            for (int i = 0; i < instSchema.Length; i++)
            {
                var operandDataType = instSchema[i];
                Operand operand;

                if (operandDataType == LiteralDataType.ConstantIndex)
                {
                    // Refer to the constant by name rather than index
                    var constantIndex = reader.ReadUInt16();

                    // TODO: Only generate name if not using a shared binary table
                    operand = new OperandReference($"const{constantIndex}", constantIndex);
                }
                else
                {
                    object literalValue = OperandLiteral.ReduceDataType(operandDataType) switch
                    {
                        LiteralDataType.Byte => reader.ReadByte(),
                        LiteralDataType.UInt16 => reader.ReadUInt16(),
                        LiteralDataType.UInt32 => reader.ReadUInt32(),
                        LiteralDataType.Int32 => reader.ReadInt32(),
                        _ => throw new InvalidOperationException($"Unexpected operand data type '{operandDataType}'")
                    };

                    operand = new OperandLiteral(literalValue, operandDataType);
                }

                operands[i] = operand;
            }

            yield return new Instruction(opCode, operands, offset);
        }
    }
}

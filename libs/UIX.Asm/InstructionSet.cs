using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System.Collections.Generic;

namespace Microsoft.Iris.Asm;

internal static class InstructionSet
{
    public static OperationType OperationMnemonicToType(string mnemonic) => OperationMnemonicMap[mnemonic.ToUpperInvariant()];
    public static OperationType? TryOperationMnemonicToType(string mnemonic)
    {
        return OperationMnemonicMap.TryGetRight(mnemonic.ToUpperInvariant(), out var type)
            ? type : null;
    }

    public static OpCode MnemonicToOpCode(string mnemonic)
    {
        if (MnemonicMap.TryGetRight(mnemonic, out var opCode))
            return opCode;
        else if (OperationMnemonicMap.TryGetRight(mnemonic, out _))
            return OpCode.Operation;

        throw new System.ArgumentException($"'{mnemonic}' is not a known UIXA instruction.", nameof(mnemonic));
    }

    public static string GetMnemonic(OpCode opCode, OperationType? opType = null)
    {
        if (opCode is OpCode.Operation && opType.HasValue && OperationMnemonicMap.TryGetLeft(opType.Value, out var operationMnemonic))
            return operationMnemonic;

        if (MnemonicMap.TryGetLeft(opCode, out var mnemonic))
            return mnemonic;

        throw new System.ArgumentException($"'{opCode}' is not a known UIXA instruction.", nameof(opCode));
    }

    internal static readonly DoubleDictionary<string, OperationType> OperationMnemonicMap =
    [
        ("ADD", OperationType.MathAdd),
        ("SUB", OperationType.MathSubtract),
        ("MUL", OperationType.MathMultiply),
        ("DIV", OperationType.MathDivide),
        ("MOD", OperationType.MathModulus),
        ("NEG", OperationType.MathNegate),
        ("AND", OperationType.LogicalAnd),
        ("ORR", OperationType.LogicalOr),
        ("NOT", OperationType.LogicalNot),
        ("REQ", OperationType.RelationalEquals),
        ("RNE", OperationType.RelationalNotEquals),
        ("RLT", OperationType.RelationalLessThan),
        ("RGT", OperationType.RelationalGreaterThan),
        ("RLE", OperationType.RelationalLessThanEquals),
        ("RGE", OperationType.RelationalGreaterThanEquals),
        ("RIS", OperationType.RelationalIs),
        ("INC", OperationType.PostIncrement),
        ("DEC", OperationType.PostDecrement),
    ];

    internal static readonly DoubleDictionary<string, OpCode> MnemonicMap =
    [
        ("COBJ", OpCode.ConstructObject),
        ("COBJI", OpCode.ConstructObjectIndirect),
        ("COBP", OpCode.ConstructObjectParam),
        ("CSTR", OpCode.ConstructFromString),
        ("CBIN", OpCode.ConstructFromBinary),
        ("INIT", OpCode.InitializeInstance),
        ("INITI", OpCode.InitializeInstanceIndirect),
        ("LSYM", OpCode.LookupSymbol),
        ("WSYM", OpCode.WriteSymbol),
        ("WSYMP", OpCode.WriteSymbolPeek),
        ("CSYM", OpCode.ClearSymbol),
        ("PINI", OpCode.PropertyInitialize),
        ("PINII", OpCode.PropertyInitializeIndirect),
        ("PLAD", OpCode.PropertyListAdd),
        ("PDAD", OpCode.PropertyDictionaryAdd),
        ("PASS", OpCode.PropertyAssign),
        ("PASST", OpCode.PropertyAssignStatic),
        ("PGET", OpCode.PropertyGet),
        ("PGETP", OpCode.PropertyGetPeek),
        ("PGETT", OpCode.PropertyGetStatic),
        ("MINV", OpCode.MethodInvoke),
        ("MINVP", OpCode.MethodInvokePeek),
        ("MINVT", OpCode.MethodInvokeStatic),
        ("MINVA", OpCode.MethodInvokePushLastParam),
        ("MINVAT", OpCode.MethodInvokeStaticPushLastParam),    // Avoid using "LT" as suffix
        ("VTC", OpCode.VerifyTypeCast),
        ("CON", OpCode.ConvertType),
        ("OPR", OpCode.Operation),                             // Generic operation, allow dynamic invocations of operators
        ("ISC", OpCode.IsCheck),
        ("ASC", OpCode.As),
        ("TYP", OpCode.TypeOf),
        ("PSHN", OpCode.PushNull),
        ("PSHC", OpCode.PushConstant),
        ("PSHT", OpCode.PushThis),
        ("DIS", OpCode.DiscardValue),
        ("RET", OpCode.ReturnValue),
        ("RETV", OpCode.ReturnVoid),
        ("JMPF", OpCode.JumpIfFalse),
        ("JMPFP", OpCode.JumpIfFalsePeek),
        ("JMPTP", OpCode.JumpIfTruePeek),
        ("JMPD", OpCode.JumpIfDictionaryContains),
        ("JMPNP", OpCode.JumpIfNullPeek),
        ("JMP", OpCode.Jump),
        ("CLIS", OpCode.ConstructListenerStorage),
        ("LIS", OpCode.Listen),
        ("LISD", OpCode.DestructiveListen),
        ("DBG", OpCode.EnterDebugState),
    ];

    private static readonly LiteralDataType[] Inst_UInt16 = [LiteralDataType.UInt16];
    private static readonly LiteralDataType[] Inst_UInt32 = [LiteralDataType.UInt32];
    private static readonly LiteralDataType[] Inst_UInt16x2 = [LiteralDataType.UInt16, LiteralDataType.UInt16];

    public static readonly Dictionary<OpCode, LiteralDataType[]> InstructionSchema = new()
    {
        [OpCode.InitializeInstanceIndirect] = [],
        [OpCode.PushNull] = [],
        [OpCode.PushThis] = [],
        [OpCode.DiscardValue] = [],
        [OpCode.ReturnValue] = [],
        [OpCode.ReturnVoid] = [],

        [OpCode.ConstructObject] = [LiteralDataType.TypeIndex],
        [OpCode.ConstructObjectIndirect] = Inst_UInt16,
        [OpCode.InitializeInstance] = [LiteralDataType.TypeIndex],
        [OpCode.LookupSymbol] = [LiteralDataType.SymbolRefIndex],
        [OpCode.WriteSymbol] = [LiteralDataType.SymbolRefIndex],
        [OpCode.WriteSymbolPeek] = [LiteralDataType.SymbolRefIndex],
        [OpCode.ClearSymbol] = [LiteralDataType.SymbolRefIndex],
        [OpCode.PropertyInitialize] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyInitializeIndirect] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyListAdd] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyAssign] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyAssignStatic] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyGet] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyGetPeek] = [LiteralDataType.PropertyIndex],
        [OpCode.PropertyGetStatic] = [LiteralDataType.PropertyIndex],
        [OpCode.MethodInvoke] = [LiteralDataType.MethodIndex],
        [OpCode.MethodInvokePeek] = [LiteralDataType.MethodIndex],
        [OpCode.MethodInvokeStatic] = [LiteralDataType.MethodIndex],
        [OpCode.MethodInvokePushLastParam] = [LiteralDataType.MethodIndex],
        [OpCode.MethodInvokeStaticPushLastParam] = [LiteralDataType.MethodIndex],
        [OpCode.VerifyTypeCast] = [LiteralDataType.TypeIndex],
        [OpCode.IsCheck] = [LiteralDataType.TypeIndex],
        [OpCode.As] = [LiteralDataType.TypeIndex],
        [OpCode.TypeOf] = [LiteralDataType.TypeIndex],
        [OpCode.PushConstant] = [LiteralDataType.ConstantIndex],
        [OpCode.ConstructListenerStorage] = Inst_UInt16,

        [OpCode.JumpIfFalse] = Inst_UInt32,
        [OpCode.JumpIfFalsePeek] = Inst_UInt32,
        [OpCode.JumpIfTruePeek] = Inst_UInt32,
        [OpCode.JumpIfNullPeek] = Inst_UInt32,
        [OpCode.Jump] = Inst_UInt32,

        [OpCode.EnterDebugState] = [LiteralDataType.Int32],

        [OpCode.ConstructObjectParam] = [LiteralDataType.TypeIndex, LiteralDataType.UInt16],
        [OpCode.ConstructFromString] = [LiteralDataType.TypeIndex, LiteralDataType.ConstantIndex],
        [OpCode.PropertyDictionaryAdd] = [LiteralDataType.TypeIndex, LiteralDataType.ConstantIndex],
        [OpCode.ConvertType] = [LiteralDataType.TypeIndex, LiteralDataType.TypeIndex],

        [OpCode.JumpIfDictionaryContains] = [LiteralDataType.PropertyIndex, LiteralDataType.UInt16, LiteralDataType.UInt32],

        [OpCode.ConstructFromBinary] = [LiteralDataType.TypeIndex, LiteralDataType.Bytes],

        [OpCode.Operation] = [LiteralDataType.OpHostIndex, LiteralDataType.Operation],

        [OpCode.Listen] = [LiteralDataType.UInt16, LiteralDataType.Byte, LiteralDataType.UInt16, LiteralDataType.UInt32],
        [OpCode.DestructiveListen] = [LiteralDataType.UInt16, LiteralDataType.Byte, LiteralDataType.UInt16, LiteralDataType.UInt32, LiteralDataType.UInt32],
    };
}
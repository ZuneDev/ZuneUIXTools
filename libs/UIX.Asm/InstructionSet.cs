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
        if (MnemonicMap.TryGetLeft(opCode, out var mnemonic))
            return mnemonic;
        else if (opType.HasValue && OperationMnemonicMap.TryGetLeft(opType.Value, out var operationMnemonic))
            return operationMnemonic;

        throw new System.ArgumentException($"'{opCode}' is not a known UIXA instruction.", nameof(opCode));
    }

    internal static readonly DoubleDictionary<string, OperationType> OperationMnemonicMap =
    [
        ("ADD", OperationType.MathAdd),
        ("SUB", OperationType.MathSubtract),
        ("MUL", OperationType.MathMultiply),
        ("DIV", OperationType.MathDivide),
        ("MOD", OperationType.MathModulus),
        ("NEG", OperationType.MathModulus),
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

        [OpCode.ConstructObject] = Inst_UInt16,
        [OpCode.ConstructObjectIndirect] = Inst_UInt16,
        [OpCode.InitializeInstance] = Inst_UInt16,
        [OpCode.LookupSymbol] = Inst_UInt16,
        [OpCode.WriteSymbol] = Inst_UInt16,
        [OpCode.WriteSymbolPeek] = Inst_UInt16,
        [OpCode.ClearSymbol] = Inst_UInt16,
        [OpCode.PropertyInitialize] = Inst_UInt16,
        [OpCode.PropertyInitializeIndirect] = Inst_UInt16,
        [OpCode.PropertyListAdd] = Inst_UInt16,
        [OpCode.PropertyAssign] = Inst_UInt16,
        [OpCode.PropertyAssignStatic] = Inst_UInt16,
        [OpCode.PropertyGet] = Inst_UInt16,
        [OpCode.PropertyGetPeek] = Inst_UInt16,
        [OpCode.PropertyGetStatic] = Inst_UInt16,
        [OpCode.MethodInvoke] = Inst_UInt16,
        [OpCode.MethodInvokePeek] = Inst_UInt16,
        [OpCode.MethodInvokeStatic] = Inst_UInt16,
        [OpCode.MethodInvokePushLastParam] = Inst_UInt16,
        [OpCode.MethodInvokeStaticPushLastParam] = Inst_UInt16,
        [OpCode.VerifyTypeCast] = Inst_UInt16,
        [OpCode.IsCheck] = Inst_UInt16,
        [OpCode.As] = Inst_UInt16,
        [OpCode.TypeOf] = Inst_UInt16,
        [OpCode.PushConstant] = Inst_UInt16,
        [OpCode.ConstructListenerStorage] = Inst_UInt16,

        [OpCode.JumpIfFalse] = Inst_UInt32,
        [OpCode.JumpIfFalsePeek] = Inst_UInt32,
        [OpCode.JumpIfTruePeek] = Inst_UInt32,
        [OpCode.JumpIfNullPeek] = Inst_UInt32,
        [OpCode.Jump] = Inst_UInt32,

        [OpCode.EnterDebugState] = [LiteralDataType.Int32],

        [OpCode.ConstructObjectParam] = Inst_UInt16x2,
        [OpCode.ConstructFromString] = Inst_UInt16x2,
        [OpCode.PropertyDictionaryAdd] = Inst_UInt16x2,
        [OpCode.ConvertType] = Inst_UInt16x2,

        [OpCode.JumpIfDictionaryContains] = [LiteralDataType.UInt16, LiteralDataType.UInt16, LiteralDataType.UInt32],

        [OpCode.ConstructFromBinary] = [LiteralDataType.UInt16, LiteralDataType.Bytes],

        [OpCode.Operation] = [LiteralDataType.UInt16, LiteralDataType.Byte],

        [OpCode.Listen] = [LiteralDataType.UInt16, LiteralDataType.Byte, LiteralDataType.UInt16, LiteralDataType.UInt32],
        [OpCode.DestructiveListen] = [LiteralDataType.UInt16, LiteralDataType.Byte, LiteralDataType.UInt16, LiteralDataType.UInt32, LiteralDataType.UInt32],
    };
}
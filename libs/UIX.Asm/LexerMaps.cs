using Microsoft.Iris.Markup;
using System.Collections.Generic;

namespace Microsoft.Iris.Asm;

internal static class LexerMaps
{
    public static OperationType OperationMnemonicToType(string mnemonic) => OperationMnemonicMap[mnemonic.ToUpperInvariant()];
    public static OperationType? TryOperationMnemonicToType(string mnemonic)
    {
        return OperationMnemonicMap.TryGetValue(mnemonic.ToUpperInvariant(), out var type)
            ? type : null;
    }

    public static OpCode MnemonicToOpCode(string mnemonic) => MnemonicMap[mnemonic.ToUpperInvariant()];

    internal static readonly IDictionary<string, OperationType> OperationMnemonicMap = new Dictionary<string, OperationType>
    {
        ["ADD"] = OperationType.MathAdd,
        ["SUB"] = OperationType.MathSubtract,
        ["MUL"] = OperationType.MathMultiply,
        ["DIV"] = OperationType.MathDivide,
        ["MOD"] = OperationType.MathModulus,
        ["NEG"] = OperationType.MathModulus,
        ["AND"] = OperationType.LogicalAnd,
        ["ORR"] = OperationType.LogicalOr,
        ["NOT"] = OperationType.LogicalNot,
        ["REQ"] = OperationType.RelationalEquals,
        ["RNE"] = OperationType.RelationalNotEquals,
        ["RLT"] = OperationType.RelationalLessThan,
        ["RGT"] = OperationType.RelationalGreaterThan,
        ["RLE"] = OperationType.RelationalLessThanEquals,
        ["RGE"] = OperationType.RelationalGreaterThanEquals,
        ["RIS"] = OperationType.RelationalIs,
        ["INC"] = OperationType.PostIncrement,
        ["DEC"] = OperationType.PostDecrement,
    };

    internal static readonly IDictionary<string, OpCode> MnemonicMap = new Dictionary<string, OpCode>
    {
        ["COBJ"] = OpCode.ConstructObject,
        ["COBI"] = OpCode.ConstructObjectIndirect,
        ["COBP"] = OpCode.ConstructObjectParam,
        ["CSTR"] = OpCode.ConstructFromString,
        ["CBIN"] = OpCode.ConstructFromBinary,
        ["INIT"] = OpCode.InitializeInstance,
        ["INID"] = OpCode.InitializeInstanceIndirect,
        ["LSYM"] = OpCode.LookupSymbol,
        ["WSYM"] = OpCode.WriteSymbol,
        ["WSYP"] = OpCode.WriteSymbolPeek,
        ["CSYM"] = OpCode.ClearSymbol,
        ["PINI"] = OpCode.PropertyInitialize,
        ["PINII"] = OpCode.PropertyInitializeIndirect,
        ["PLAD"] = OpCode.PropertyListAdd,
        ["PDAD"] = OpCode.PropertyDictionaryAdd,
        ["PASS"] = OpCode.PropertyAssign,
        ["PASST"] = OpCode.PropertyAssignStatic,
        ["PGET"] = OpCode.PropertyGet,
        ["PGETP"] = OpCode.PropertyGetPeek,
        ["PGETT"] = OpCode.PropertyGetStatic,
        ["MINV"] = OpCode.MethodInvoke,
        ["MINVP"] = OpCode.MethodInvokePeek,
        ["MINVT"] = OpCode.MethodInvokeStatic,
        ["MINVA"] = OpCode.MethodInvokePushLastParam,
        ["MINVAT"] = OpCode.MethodInvokeStaticPushLastParam,    // Avoid using "LT" as suffix
        ["VTC"] = OpCode.VerifyTypeCast,
        ["CON"] = OpCode.ConvertType,
        ["OPR"] = OpCode.Operation,                             // Generic operation, allow dynamic invocations of operators
        ["ADD"] = OpCode.Operation,
        ["SUB"] = OpCode.Operation,
        ["MUL"] = OpCode.Operation,
        ["DIV"] = OpCode.Operation,
        ["MOD"] = OpCode.Operation,
        ["NEG"] = OpCode.Operation,
        ["AND"] = OpCode.Operation,
        ["ORR"] = OpCode.Operation,
        ["NOT"] = OpCode.Operation,
        ["REQ"] = OpCode.Operation,
        ["RNE"] = OpCode.Operation,
        ["RLT"] = OpCode.Operation,
        ["RGT"] = OpCode.Operation,
        ["RLE"] = OpCode.Operation,
        ["RGE"] = OpCode.Operation,
        ["RIS"] = OpCode.Operation,
        ["INC"] = OpCode.Operation,
        ["DEC"] = OpCode.Operation,
        ["ISC"] = OpCode.IsCheck,
        ["ASC"] = OpCode.As,
        ["TYP"] = OpCode.TypeOf,
        ["PSHN"] = OpCode.PushNull,
        ["PSHC"] = OpCode.PushConstant,
        ["PSHT"] = OpCode.PushThis,
        ["DIS"] = OpCode.DiscardValue,
        ["RET"] = OpCode.ReturnValue,
        ["RETV"] = OpCode.ReturnVoid,
        ["JMPF"] = OpCode.JumpIfFalse,
        ["JMPFP"] = OpCode.JumpIfFalsePeek,
        ["JMPTP"] = OpCode.JumpIfTruePeek,
        ["JMPD"] = OpCode.JumpIfDictionaryContains,
        ["JMPNP"] = OpCode.JumpIfNullPeek,
        ["JMP"] = OpCode.Jump,
        ["CLIS"] = OpCode.ConstructListenerStorage,
        ["LIS"] = OpCode.Listen,
        ["DLS"] = OpCode.DestructiveListen,
        ["DBG"] = OpCode.EnterDebugState,
    };
}
using Humanizer;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;

namespace Microsoft.Iris.Asm;

public class Disassembler
{
    private readonly MarkupLoadResult _loadResult;

    private Disassembler(MarkupLoadResult loadResult)
    {
        _loadResult = loadResult;
    }

    public static Disassembler Load(MarkupLoadResult loadResult) => new(loadResult);

    public IEnumerable<IImport> GetImports()
    {
        foreach (var typeImport in _loadResult.ImportTables.TypeImports)
        {
            var uri = typeImport.Owner.Uri;

            // Skip default UIX namespace
            if (uri == "http://schemas.microsoft.com/2007/uix")
                continue;

            // Skip self
            if (uri == _loadResult.Uri)
                continue;

            string name = uri;
            var schemeLength = uri.IndexOf("://");
            if (schemeLength > 0)
            {
                var scheme = uri[..schemeLength];
                if (scheme == "assembly")
                {
                    // Assume the URI represents a C# namespace
                    name = uri.Split('.', '/', '\\')[^1];

                    // Remove the extra assembly info
                    uri = uri.Split(',')[0];
                }
                else
                {
                    // Assume the URI represents a file,
                    // skip the extension
                    name = uri.Split('.', '/', '\\')[^2];
                }
            }

            yield return new NamespaceImport(uri, name.Camelize());
        };
    }

    public IEnumerable<IBodyItem> GetBody()
    {
        var reader = _loadResult.ObjectSection;

        // Insert a label to mark the start of the object section.
        // In the future, this might use a special directive like `.section object`
        yield return new Label("code");

        while (reader.CurrentOffset < reader.Size)
        {
            var opCode = (OpCode)reader.ReadByte();
            
            switch (opCode)
            {
                // CMD (No operands)
                case OpCode.InitializeInstanceIndirect:         // INITI
                case OpCode.PushNull:                           // PSHN
                case OpCode.PushThis:                           // PSHT
                case OpCode.DiscardValue:                       // DIS
                case OpCode.ReturnValue:                        // RET
                case OpCode.ReturnVoid:                         // RETV
                    yield return new Instruction(opCode, []);
                    break;

                // CMD <UInt16>
                case OpCode.ConstructObject:                    // COBJ <typeIndex>
                case OpCode.ConstructObjectIndirect:            // COBJI <assignmentTypeIndex>
                case OpCode.InitializeInstance:                 // INIT <typeIndex>
                case OpCode.LookupSymbol:                       // LSYM <symbolRefIndex>
                case OpCode.WriteSymbol:                        // WSYM <symbolRefIndex>
                case OpCode.WriteSymbolPeek:                    // WSYMP <symbolRefIndex>
                case OpCode.ClearSymbol:                        // CSYM <symbolRefIndex>
                case OpCode.PropertyInitialize:                 // PINI <propertyIndex>
                case OpCode.PropertyInitializeIndirect:         // PINII <propertyIndex>
                case OpCode.PropertyListAdd:                    // PLAD <propertyIndex>
                case OpCode.PropertyAssign:                     // PASS <propertyIndex>
                case OpCode.PropertyAssignStatic:               // PASST <propertyIndex>
                case OpCode.PropertyGet:                        // PGET <propertyIndex>
                case OpCode.PropertyGetPeek:                    // PGETP <propertyIndex>
                case OpCode.PropertyGetStatic:                  // PGETT <propertyIndex>
                case OpCode.MethodInvoke:                       // MINV <methodIndex>
                case OpCode.MethodInvokePeek:                   // MINVP <methodIndex>
                case OpCode.MethodInvokeStatic:                 // MINVT <methodIndex>
                case OpCode.MethodInvokePushLastParam:          // MINVA <methodIndex>
                case OpCode.MethodInvokeStaticPushLastParam:    // MINVAT <methodIndex>
                case OpCode.VerifyTypeCast:                     // VTC <typeIndex>
                case OpCode.IsCheck:                            // ISC <targetTypeIndex>
                case OpCode.As:                                 // ASC <targetTypeIndex>
                case OpCode.TypeOf:                             // TYP <typeIndex>
                case OpCode.PushConstant:                       // PSHC <constantIndex>
                case OpCode.ConstructListenerStorage:           // CLIS <listenerCount>
                    yield return new Instruction(opCode, [new(reader.ReadUInt16())]);
                    break;

                // CMD <UInt32>
                case OpCode.JumpIfFalse:                        // JMPF <jumpTo>
                case OpCode.JumpIfFalsePeek:                    // JMPFP <jumpTo>
                case OpCode.JumpIfTruePeek:                     // JMPT <jumpTo>
                case OpCode.JumpIfNullPeek:                     // JMPNP <jumpTo>
                case OpCode.Jump:                               // JMP <jumpTo>
                    yield return new Instruction(opCode, [new(reader.ReadUInt32())]);
                    break;

                // CMD <Int32>
                case OpCode.EnterDebugState:                    // DBG <breakpointIndex>
                    yield return new Instruction(opCode, [new(reader.ReadInt32())]);
                    break;

                // CMD <UInt16> <UInt16>
                case OpCode.ConstructObjectParam:               // COBP <targetTypeIndex> <constructorIndex>
                case OpCode.ConstructFromString:                // CSTR <typeIndex> <stringIndex>
                case OpCode.PropertyDictionaryAdd:              // PDAD <propertyIndex> <keyIndex>
                case OpCode.ConvertType:                        // CON <toTypeIndex> <fromTypeIndex>
                    yield return new Instruction(opCode, [new(reader.ReadUInt16()), new(reader.ReadUInt16())]);
                    break;

                case OpCode.JumpIfDictionaryContains:           // JMPD <propertyIndex> <keyIndex> <jumpTo>
                    yield return new Instruction(opCode, [new(reader.ReadUInt16()), new(reader.ReadUInt16()), new(reader.ReadUInt32())]);
                    break;

                case OpCode.ConstructFromBinary:                // CBIN <typeIndex> <object>
                    var cbinTypeIndex = reader.ReadUInt16();
                    TypeSchema cbinTypeSchema = _loadResult.ImportTables.TypeImports[cbinTypeIndex];
                    object cbinObject = cbinTypeSchema.DecodeBinary(reader);

                    yield return new Instruction(opCode, [new(cbinTypeIndex), new(cbinObject)]);
                    break;

                case OpCode.Operation:                          // OPR <opHostIndex> <operation>
                    var opHostIndex = reader.ReadUInt16();
                    var op = (OperationType)reader.ReadByte();

                    yield return new Instruction(opCode, op, [new(opHostIndex)]);
                    break;

                case OpCode.Listen:                             // LIS <listenerIndex> <listenerType> <watchIndex> <handlerOffset>
                case OpCode.DestructiveListen:                  // LISD <listenerIndex> <listenerType> <watchIndex> <handlerOffset> <refreshOffset>
                    List<Operand> lisOperands = [new(reader.ReadUInt16()), new(reader.ReadByte()),
                        new(reader.ReadUInt16()), new(reader.ReadUInt32())];

                    if (opCode == OpCode.DestructiveListen)
                        lisOperands.Add(new(reader.ReadUInt32()));

                    yield return new Instruction(opCode, lisOperands);
                    break;

                default:
                    throw new NotImplementedException($"The {opCode} instruction has not been implemented yet.");
            }
        }

        yield break;
    }

    public string Write()
    {
        _loadResult.Load(LoadPass.DeclareTypes);
        _loadResult.Load(LoadPass.PopulatePublicModel);
        _loadResult.Load(LoadPass.Full);
        _loadResult.Load(LoadPass.Done);

        List<IImport> imports = new(GetImports());
        List<IBodyItem> body = new(GetBody());

        Program asmProgram = new(imports, body);
        return asmProgram.ToString();
    }
}

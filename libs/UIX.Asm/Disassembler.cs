using Humanizer;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.Asm;

public class Disassembler
{
    private readonly MarkupLoadResult _loadResult;
    private readonly Dictionary<string, string> _importedUris;
    private readonly Dictionary<uint, List<Label>> _offsetLabelMap = new();

    private Disassembler(MarkupLoadResult loadResult)
    {
        _loadResult = loadResult;
        _importedUris = new()
        {
            [_loadResult.Uri] = "me",
            ["http://schemas.microsoft.com/2007/uix"] = null
        };
    }

    public static Disassembler Load(MarkupLoadResult loadResult) => new(loadResult);

    public IEnumerable<Directive> GetExports()
    {
        foreach (var typeSchema in _loadResult.ExportTable)
        {
            var labelPrefix = typeSchema.Name;
            var baseName = GetQualifiedName(typeSchema.Base);

            if (typeSchema is not MarkupTypeSchema markupTypeSchema)
                throw new Exception($"Disassembler failed to disassemble export {typeSchema}, '{typeSchema.GetType()}' is not supported.");

            yield return new ExportDirective(labelPrefix, markupTypeSchema.ListenerCount, markupTypeSchema.Base.Name);

            var propOffset = markupTypeSchema.InitializePropertiesOffset;
            InsertLabel(propOffset, ExportDirective.GetInitializePropertiesLabel(labelPrefix));

            var loclOffset = markupTypeSchema.InitializeLocalsInputOffset;
            InsertLabel(loclOffset, ExportDirective.GetInitializeLocalsInputLabel(labelPrefix));

            var contOffset = markupTypeSchema.InitializeContentOffset;
            InsertLabel(contOffset, ExportDirective.GetInitializeContentLabel(labelPrefix));

            if (markupTypeSchema.InitialEvaluateOffsets != null)
            {
                for (int i = 0; i < markupTypeSchema.InitialEvaluateOffsets.Length; i++)
                {
                    uint offset = markupTypeSchema.InitialEvaluateOffsets[i];
                    var labelName = $"{ExportDirective.GetInitializeContentLabel(labelPrefix)}{i:N}";

                    InsertLabel(offset, labelName);
                }
            }

            if (markupTypeSchema.FinalEvaluateOffsets != null)
            {
                for (int i = 0; i < markupTypeSchema.FinalEvaluateOffsets.Length; i++)
                {
                    uint offset = markupTypeSchema.FinalEvaluateOffsets[i];
                    var labelName = $"{ExportDirective.GetFinalEvaluateOffsetsLabelPrefix(labelPrefix)}{i:N}";

                    InsertLabel(offset, labelName);
                }

            }

            if (markupTypeSchema.RefreshGroupOffsets != null)
            {
                for (int i = 0; i < markupTypeSchema.RefreshGroupOffsets.Length; i++)
                {
                    uint offset = markupTypeSchema.RefreshGroupOffsets[i];
                    var labelName = $"{ExportDirective.GetRefreshGroupOffsetsLabelPrefix(labelPrefix)}{i:N}";
                    InsertLabel(offset, labelName);
                }
            }
        }
    }

    public IEnumerable<IImport> GetImports()
    {
        // Ues _importedUris to keep track of what has already been imported.
        // Skip self and default UIX namespace.

        foreach (var typeImport in _loadResult.ImportTables.TypeImports)
        {
            var uri = typeImport.Owner.Uri;
            if (!_importedUris.TryGetValue(uri, out var namespacePrefix))
            {
                namespacePrefix = uri;
                var schemeLength = uri.IndexOf("://");
                if (schemeLength > 0)
                {
                    var scheme = uri[..schemeLength];
                    if (scheme == "assembly")
                    {
                        // Assume the URI represents a C# namespace
                        namespacePrefix = uri.Split('.', '/', '\\', '!')[^1];

                        // Remove the extra assembly info
                        uri = uri.Split(',')[0];
                    }
                    else
                    {
                        // Assume the URI represents a file,
                        // skip the extension
                        namespacePrefix = uri.Split('.', '/', '\\', '!')[^2];
                    }
                }

                namespacePrefix = namespacePrefix.Camelize();
                _importedUris.Add(uri, namespacePrefix);

                yield return new NamespaceImport(uri, namespacePrefix);
            }

            yield return new TypeImport(namespacePrefix, typeImport.Name);
        };
    }

    public IEnumerable<IBodyItem> GetBody()
    {
        var reader = _loadResult.ObjectSection;

        // Insert a label to mark the start of the object section.
        yield return new SectionDirective("object");

        while (reader.CurrentOffset < reader.Size)
        {
            if (_offsetLabelMap.TryGetValue(reader.CurrentOffset, out var labels))
                foreach (var label in labels)
                    yield return label;

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
                    yield return Instruction.CreateWithSchema(opCode);
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
                    yield return Instruction.CreateWithSchema(opCode, reader.ReadUInt16());
                    break;

                // CMD <UInt32>
                case OpCode.JumpIfFalse:                        // JMPF <jumpTo>
                case OpCode.JumpIfFalsePeek:                    // JMPFP <jumpTo>
                case OpCode.JumpIfTruePeek:                     // JMPT <jumpTo>
                case OpCode.JumpIfNullPeek:                     // JMPNP <jumpTo>
                case OpCode.Jump:                               // JMP <jumpTo>
                    yield return Instruction.CreateWithSchema(opCode, reader.ReadUInt32());
                    break;

                // CMD <Int32>
                case OpCode.EnterDebugState:                    // DBG <breakpointIndex>
                    yield return Instruction.CreateWithSchema(opCode, reader.ReadInt32());
                    break;

                // CMD <UInt16> <UInt16>
                case OpCode.ConstructObjectParam:               // COBP <targetTypeIndex> <constructorIndex>
                case OpCode.ConstructFromString:                // CSTR <typeIndex> <stringIndex>
                case OpCode.PropertyDictionaryAdd:              // PDAD <propertyIndex> <keyIndex>
                case OpCode.ConvertType:                        // CON <toTypeIndex> <fromTypeIndex>
                    yield return Instruction.CreateWithSchema(opCode, reader.ReadUInt16(), reader.ReadUInt16());
                    break;

                case OpCode.JumpIfDictionaryContains:           // JMPD <propertyIndex> <keyIndex> <jumpTo>
                    yield return Instruction.CreateWithSchema(opCode, reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt32());
                    break;

                case OpCode.ConstructFromBinary:                // CBIN <typeIndex> <object>
                    var cbinTypeIndex = reader.ReadUInt16();
                    TypeSchema cbinTypeSchema = _loadResult.ImportTables.TypeImports[cbinTypeIndex];
                    object cbinObject = cbinTypeSchema.DecodeBinary(reader);

                    yield return Instruction.CreateWithSchema(opCode, cbinTypeIndex, cbinObject);
                    break;

                case OpCode.Operation:                          // OPR <opHostIndex> <operation>
                    var opHostIndex = reader.ReadUInt16();
                    var op = (OperationType)reader.ReadByte();

                    yield return Instruction.CreateWithSchema(opCode, opHostIndex, op);
                    break;

                case OpCode.Listen:                             // LIS <listenerIndex> <listenerType> <watchIndex> <handlerOffset>
                case OpCode.DestructiveListen:                  // LISD <listenerIndex> <listenerType> <watchIndex> <handlerOffset> <refreshOffset>
                    var listenerIndex = reader.ReadUInt16();
                    var listenerType = reader.ReadByte();
                    var watchIndex = reader.ReadUInt16();
                    var handlerOffset = reader.ReadUInt32();

                    if (opCode == OpCode.DestructiveListen)
                        yield return Instruction.CreateWithSchema(opCode, listenerIndex, listenerType, watchIndex, handlerOffset, reader.ReadUInt32());
                    else
                        yield return Instruction.CreateWithSchema(opCode, listenerIndex, listenerType, watchIndex, handlerOffset);

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

        List<IDirective> directives = GetImports().Cast<IDirective>()
            .Concat(GetExports())
            .ToList();
        List<IBodyItem> body = new(GetBody());

        Program asmProgram = new(directives, body);
        return asmProgram.ToString();
    }

    private string GetQualifiedName(TypeSchema schema)
    {
        _importedUris.TryGetValue(schema.Owner.Uri, out string prefix);

        return prefix != null
            ? $"{prefix}:{schema.Name}"
            : schema.Name;
    }

    private void InsertLabel(uint offset, string labelName)
    {
        if (!_offsetLabelMap.TryGetValue(offset, out var labels))
            labels = _offsetLabelMap[offset] = new(1);
        labels.Add(new(labelName));
    }
}

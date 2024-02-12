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

            if (typeSchema is not MarkupTypeSchema markupTypeSchema)
                throw new Exception($"Disassembler failed to disassemble export {typeSchema}, '{typeSchema.GetType()}' is not supported.");

            var baseName = markupTypeSchema.MarkupType.ToString();
            yield return new ExportDirective(labelPrefix, markupTypeSchema.ListenerCount, baseName);

            var propOffset = markupTypeSchema.InitializePropertiesOffset;
            if (propOffset != uint.MaxValue)
                InsertLabel(propOffset, ExportDirective.GetInitializePropertiesLabel(labelPrefix));

            var loclOffset = markupTypeSchema.InitializeLocalsInputOffset;
            if (loclOffset != uint.MaxValue)
                InsertLabel(loclOffset, ExportDirective.GetInitializeLocalsInputLabel(labelPrefix));

            var contOffset = markupTypeSchema.InitializeContentOffset;
            if (contOffset != uint.MaxValue)
                InsertLabel(contOffset, ExportDirective.GetInitializeContentLabel(labelPrefix));

            if (markupTypeSchema.InitialEvaluateOffsets != null)
            {
                for (int i = 0; i < markupTypeSchema.InitialEvaluateOffsets.Length; i++)
                {
                    uint offset = markupTypeSchema.InitialEvaluateOffsets[i];
                    var labelName = $"{ExportDirective.GetInitializeContentLabel(labelPrefix)}{i:D}";

                    InsertLabel(offset, labelName);
                }
            }

            if (markupTypeSchema.FinalEvaluateOffsets != null)
            {
                for (int i = 0; i < markupTypeSchema.FinalEvaluateOffsets.Length; i++)
                {
                    uint offset = markupTypeSchema.FinalEvaluateOffsets[i];
                    var labelName = $"{ExportDirective.GetFinalEvaluateOffsetsLabelPrefix(labelPrefix)}{i:D}";

                    InsertLabel(offset, labelName);
                }
            }

            if (markupTypeSchema.RefreshGroupOffsets != null)
            {
                for (int i = 0; i < markupTypeSchema.RefreshGroupOffsets.Length; i++)
                {
                    uint offset = markupTypeSchema.RefreshGroupOffsets[i];
                    var labelName = $"{ExportDirective.GetRefreshGroupOffsetsLabelPrefix(labelPrefix)}{i:D}";
                    InsertLabel(offset, labelName);
                }
            }
        }
    }

    public IEnumerable<IImportDirective> GetImports()
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
                        // Assume 'host' is an assembly name and path represents a C# namespace
                        var assemblyUriParts = uri.Split('/');

                        var importedNamespace = assemblyUriParts[^1];
                        namespacePrefix = importedNamespace.Split('.', '/', '\\', '!')[^1];

                        System.Reflection.AssemblyName assemblyName = new(assemblyUriParts[^2]);
                        uri = $"{scheme}://{assemblyName.Name}/{importedNamespace}";
                    }
                    else
                    {
                        // Assume the URI represents a file,
                        // skip the extension
                        namespacePrefix = uri.Split('.', '/', '\\', '!')[^2];
                    }
                }

                // Some imports, such as assembly imports, require additional parsing
                // and might change the URI that actually gets imported.
                if (_importedUris.ContainsKey(uri))
                    continue;

                namespacePrefix = namespacePrefix.Camelize();
                _importedUris.Add(uri, namespacePrefix);

                yield return new NamespaceImport(uri, namespacePrefix);
            }

            yield return new TypeImport(new(namespacePrefix, typeImport.Name));
        };
    }

    public IEnumerable<ConstantDirective> GetConstants()
    {
        var constantsTable = _loadResult.ConstantsTable;

        List<(int c, TypeSchema typeSchema, object constantValue)> constants = new();

        bool canUsePersistList = constantsTable.PersistList is not null;
        if (canUsePersistList)
        {
            var persistedList = _loadResult.ConstantsTable.PersistList;

            for (int c = 0; c < persistedList.Length; c++)
            {
                var persistedConstant = persistedList[c];
                var typeSchema = persistedConstant.Type;

                constants.Add((c, typeSchema, persistedConstant));
            }
        }
        else
        {
            // UIB doesn't persist constants, so we have to use an alternate, slower method
            for (int c = 0; ; c++)
            {
                object constantValue;
                try
                {
                    constantValue = constantsTable.Get(c);
                }
                catch
                {
                    break;
                }

                var runtimeType = constantValue.GetType();
                var typeSchema = _loadResult.ImportTables.TypeImports.FirstOrDefault(t => t.RuntimeType == runtimeType);

                constants.Add((c, typeSchema, constantValue));
            }
        }

        foreach (var (c, typeSchema, constantValue) in constants)
        {
            string encodedValue = constantValue is IStringEncodable encodable
                ? encodable.EncodeString()
                : constantValue.ToString();

            QualifiedTypeName qualifiedTypeName = GetQualifiedName(typeSchema);

            yield return new ConstantDirective($"const{c:D}", qualifiedTypeName, encodedValue);
        }
    }

    public IEnumerable<IBodyItem> GetCode()
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

                    operand = new OperandReference($"const{constantIndex}");
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

            yield return new Instruction(opCode, operands);
        }

        yield break;
    }

    public string Write()
    {
        _loadResult.Load(LoadPass.DeclareTypes);
        _loadResult.Load(LoadPass.PopulatePublicModel);
        _loadResult.Load(LoadPass.Full);
        _loadResult.Load(LoadPass.Done);

        List<IEnumerable<IBodyItem>> segments = [
            GetExports(),
            GetImports(),
            GetConstants(),
            GetCode(),
        ];

        List<IBodyItem> body = [];
        foreach (var segment in segments)
            body.AddRange(segment);

        Program asmProgram = new(body);
        return asmProgram.ToString();
    }

    private QualifiedTypeName GetQualifiedName(TypeSchema schema)
    {
        _importedUris.TryGetValue(schema.Owner.Uri, out string prefix);
        return new(prefix, schema.Name);
    }

    private void InsertLabel(uint offset, string labelName)
    {
        if (!_offsetLabelMap.TryGetValue(offset, out var labels))
            labels = _offsetLabelMap[offset] = new(1);
        labels.Add(new(labelName));
    }
}

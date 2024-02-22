using Humanizer;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

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
                        var path = uri[(schemeLength + 3)..];
                        var nsIndex = path.IndexOf('/');
                        if (nsIndex >= 0)
                        {
                            var importedNamespace = path[(nsIndex + 1)..];
                            namespacePrefix = importedNamespace.Split('.', '/', '\\', '!')[^1];

                            System.Reflection.AssemblyName assemblyName = new(path[..nsIndex]);
                            uri = $"assembly://{assemblyName.Name}/{importedNamespace}";
                        }
                        else
                        {
                            // No namespace was specified, assume we're importing the whole assembly
                            System.Reflection.AssemblyName assemblyName = new(path);
                            namespacePrefix = assemblyName.Name;

                            uri = $"assembly://{assemblyName.Name}";
                        }
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

        foreach (var constructorImport in _loadResult.ImportTables.ConstructorImports)
        {
            var typeName = GetQualifiedName(constructorImport.Owner);
            var parameterTypeNames = constructorImport.ParameterTypes.Select(GetQualifiedName);

            yield return new ConstructorImport(typeName, parameterTypeNames);
        }

        foreach (var methodImport in _loadResult.ImportTables.MethodImports)
        {
            var typeName = GetQualifiedName(methodImport.Owner);
            var parameterTypeNames = methodImport.ParameterTypes.Select(GetQualifiedName);

            yield return new MethodImport(typeName, methodImport.Name, parameterTypeNames);
        }

        Dictionary<QualifiedTypeName, List<string>> namedMemberImports = new();
        void GroupMemberImport(TypeSchema owner, string memberName)
        {
            var typeName = GetQualifiedName(owner);
            if (!namedMemberImports.TryGetValue(typeName, out var currentTypeImports))
                currentTypeImports = namedMemberImports[typeName] = new();
            currentTypeImports.Add(memberName);
        }

        foreach (var propertyImport in _loadResult.ImportTables.PropertyImports)
            GroupMemberImport(propertyImport.Owner, propertyImport.Name);

        foreach (var eventImport in _loadResult.ImportTables.EventImports)
            GroupMemberImport(eventImport.Owner, eventImport.Name);

        foreach (var groupedImport in namedMemberImports)
            yield return new NamedMemberImport(groupedImport.Key, groupedImport.Value);
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

                constants.Add((c, typeSchema, persistedConstant.Data));
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
                var typeSchema = _loadResult.ImportTables.TypeImports.FirstOrDefault(t => t.RuntimeType == runtimeType)
                    ?? _loadResult.ImportTables.TypeImports.FirstOrDefault(t => t.RuntimeType == runtimeType.BaseType)
                    ?? throw new Exception($"Failed to find type schema for '{runtimeType.Name}' in {_loadResult.Uri}");

                constants.Add((c, typeSchema, constantValue));
            }
        }

        var stringTypeSchema = UIXTypes.MapIDToType(UIXTypeID.String);

        foreach (var (c, typeSchema, constantValue) in constants)
        {
            var constantName = $"const{c:D}";
            QualifiedTypeName qualifiedTypeName = GetQualifiedName(typeSchema);
            
            if (constantValue is IStringEncodable encodable)
            {
                var encodedValue = encodable.EncodeString();
                yield return new StringEncodedConstantDirective(constantName, qualifiedTypeName, encodedValue);
            }
            else if (constantValue is Layout.ILayout constantLayout && Layout.PredefinedLayouts.TryConvertToString(constantLayout, out var constantLayoutString))
            {
                qualifiedTypeName = GetQualifiedName(UIXTypes.MapIDToType(UIXTypeID.Layout));
                yield return new StringEncodedConstantDirective(constantName, qualifiedTypeName, constantLayoutString);
            }
            else if (typeSchema.SupportsTypeConversion(stringTypeSchema))
            {
                var encodedValue = constantValue.ToString();
                yield return new StringEncodedConstantDirective(constantName, qualifiedTypeName, encodedValue);
            }
            else if (typeSchema.SupportsBinaryEncoding)
            {
                ByteCodeWriter writer = new();
                typeSchema.EncodeBinary(writer, constantValue);

                var reader = writer.CreateReader();
                byte[] encodedBytes = new byte[reader.Size];
                Marshal.Copy(reader.GetAddress(0), encodedBytes, 0, (int)reader.Size);

                yield return new BinaryEncodedConstantDirective(constantName, qualifiedTypeName, encodedBytes);
            }
            else
            {
                XName constElemName = qualifiedTypeName.NamespacePrefix is null
                    ? qualifiedTypeName.TypeName
                    : XName.Get(qualifiedTypeName.TypeName, qualifiedTypeName.NamespacePrefix);
                XElement constElem = new(constElemName);

                var defaultConstantValue = typeSchema.ConstructDefault();

                foreach (var prop in typeSchema.Properties)
                {
                    // No need to serialize properties that can't be set
                    if (!prop.CanWrite)
                        continue;

                    var defaultPropValue = prop.GetValue(defaultConstantValue);
                    var propValue = prop.GetValue(constantValue);

                    var encodedPropValue = EncodeSimpleConstant(propValue);
                    var encodedDefaultPropValue = EncodeSimpleConstant(defaultPropValue);

                    // No need to serialize properties that are at their default value
                    if (encodedPropValue == encodedDefaultPropValue)
                        continue;

                    constElem.SetAttributeValue(prop.Name, encodedPropValue);
                }

                var constructor = constElem.ToString(SaveOptions.DisableFormatting);
                yield return new ConstantDirective(constantName, qualifiedTypeName, constructor);
            }
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
        _loadResult.FullLoad();

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

    private static string EncodeSimpleConstant(object value)
    {
        return value is IStringEncodable encodable
            ? encodable.EncodeString()
            : value.ToString();
    }
}

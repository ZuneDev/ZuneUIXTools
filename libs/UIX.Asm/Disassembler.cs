using Humanizer;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Iris.Asm;

public class Disassembler
{
    private Dictionary<int, string> _constantsTable = [];
    private Program _program = null;

    private readonly MarkupLoadResult _loadResult;
    private readonly MarkupLoadResult _dataTableLoadResult;
    private readonly Dictionary<string, string> _importedUris;
    private readonly Dictionary<uint, List<Label>> _offsetLabelMap = new();
    
    private static readonly TypeSchema _stringTypeSchema = UIXTypes.MapIDToType(UIXTypeID.String);

    private Disassembler(MarkupLoadResult loadResult, MarkupLoadResult dataTableLoadResult = null)
    {
        _loadResult = loadResult;

        _dataTableLoadResult = dataTableLoadResult
            ?? loadResult.BinaryDataTable?.SharedDependenciesTableWithBinaryDataTable?.FirstOrDefault() as MarkupLoadResult;

        _importedUris = new()
        {
            [_loadResult.Uri] = "me",
            ["http://schemas.microsoft.com/2007/uix"] = null
        };
    }

    public static Disassembler Load(LoadResult loadResult, LoadResult dataTableLoadResult = null)
    {
        if (loadResult is not MarkupLoadResult markupLoadResult)
            throw new ArgumentException($"Disassembly can only be performed on markup. Expected '{nameof(MarkupLoadResult)}', got '{loadResult?.GetType().Name}'.", nameof(loadResult));

        if (dataTableLoadResult is MarkupLoadResult dataTableMarkupLoadResult)
            return new(markupLoadResult, dataTableMarkupLoadResult);

        if (dataTableLoadResult is not null)
            throw new ArgumentException($"Data table must be markup. Expected '{nameof(MarkupLoadResult)}', got '{dataTableLoadResult?.GetType().Name}'.", nameof(dataTableLoadResult));

        return new(markupLoadResult);
    }

    public Program Disassemble()
    {
        if (_program is null)
        {
            _loadResult.FullLoad();
            if (_loadResult.Status == LoadResultStatus.Error)
                throw new Exception($"Failed to load '{_loadResult.ErrorContextUri}'");

            if (UseSharedDataTable)
            {
                _dataTableLoadResult.FullLoad();
                if (_dataTableLoadResult.Status == LoadResultStatus.Error)
                    throw new Exception($"Failed to load '{_dataTableLoadResult.ErrorContextUri}'");
            }

            List<IEnumerable<IBodyItem>> segments = [
                GetExports(),
                GetImports(),
                GetConstants(),
                GetCode(),
            ];

            List<IBodyItem> body = [];
            foreach (var segment in segments)
                body.AddRange(segment);

            if (UseSharedDataTable)
                body.Add(new SharedDataTableDirective(_dataTableLoadResult.Uri));

            _program = new(body);
        }

        return _program;
    }

    public string Write() => Disassemble().ToString();

    [MemberNotNullWhen(true, nameof(_dataTableLoadResult))]
    private bool UseSharedDataTable => _dataTableLoadResult is not null;

    private IEnumerable<ExportDirective> GetExports()
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

    private IEnumerable<IImportDirective> GetImports() => EnumerateImports().Distinct();

    private IEnumerable<IImportDirective> EnumerateImports()
    {
        if (UseSharedDataTable)
            yield break;

        // Ues _importedUris to keep track of what has already been imported.
        // Skip self and default UIX namespace.

        foreach (var typeImport in _loadResult.ImportTables.TypeImports)
        {
            var typeName = typeImport.Name;
            var uri = typeImport.Owner.Uri;
            if (!_importedUris.TryGetValue(uri, out var namespacePrefix))
            {
                var baseNamespacePrefix = uri;

                var ownerUri = uri;
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
                            baseNamespacePrefix = importedNamespace.Split('.', '/', '\\', '!')[^1];

                            System.Reflection.AssemblyName assemblyName = new(path[..nsIndex]);
                            uri = $"assembly://{assemblyName.Name}/{importedNamespace}";
                        }
                        else
                        {
                            // No namespace was specified, assume we're importing the whole assembly
                            System.Reflection.AssemblyName assemblyName = new(path);
                            baseNamespacePrefix = assemblyName.Name;

                            uri = $"assembly://{assemblyName.Name}/";
                        }
                    }
                    else
                    {
                        // Assume the URI represents a file,
                        // skip the extension
                        baseNamespacePrefix = uri.Split('.', '/', '\\', '!')[^2];
                    }
                }

                baseNamespacePrefix = baseNamespacePrefix.Camelize();
                namespacePrefix = baseNamespacePrefix;

                // Some imports, such as assembly imports, require additional parsing
                // and might change the URI that actually gets imported.
                if (!_importedUris.ContainsKey(uri))
                {
                    // Prevent similar imports from generating the same prefix
                    int duplicateCount = 0;
                    while (_importedUris.ContainsValue(namespacePrefix))
                        namespacePrefix = $"{baseNamespacePrefix}{++duplicateCount}";

                    _importedUris.Add(uri, namespacePrefix);

                    yield return new NamespaceImport(uri, namespacePrefix);
                }

                // Ensure that the original, un-normalized URI is saved too
                if (!_importedUris.ContainsKey(ownerUri))
                    _importedUris.Add(ownerUri, namespacePrefix);
            }

            yield return new TypeImport(new(namespacePrefix, typeName));
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

    private IEnumerable<IBodyItem> GetCode()
    {
        var reader = _loadResult.ObjectSection;

        // Insert a label to mark the start of the object section.
        yield return new SectionDirective("object");

        foreach (var codeItem in ObjectSection.Decode(reader, _offsetLabelMap))
            yield return codeItem;
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

    private IEnumerable<ConstantDirective> GetConstants()
    {
        if (UseSharedDataTable)
        {
            // Constants have already been disassembled from the shared binary table
            var asmConstants = _dataTableLoadResult is AsmMarkupLoadResult asmDataTableLoadResult
                ? asmDataTableLoadResult.Loader.Program.Directives.OfType<ConstantDirective>().ToList()
                : null;

            foreach (var info in EnumerateConstantInfo(_dataTableLoadResult))
            {
                var constantName = asmConstants?[info.Index]?.Name
                    ?? info.GenerateDefaultName();

                _constantsTable[info.Index] = constantName;
            }

            yield break;
        }
        else
        {
            foreach (var info in EnumerateConstantInfo(_loadResult))
            {
                var constantName = info.GenerateDefaultName();
                _constantsTable[info.Index] = constantName;
                yield return EncodeConstant(info.Value, constantName, info.Type);
            }
        }
    }

    public static IEnumerable<RawConstantInfo> EnumerateConstantInfo(MarkupLoadResult loadResult)
    {
        var constantsTable = loadResult.ConstantsTable;
        bool hasPersistList = constantsTable.PersistList is not null;

        if (hasPersistList)
        {
            var persistedList = loadResult.ConstantsTable.PersistList;

            for (int c = 0; c < persistedList.Length; c++)
            {
                var persistedConstant = persistedList[c];
                var typeSchema = persistedConstant.Type;

               yield return new(c, typeSchema, persistedConstant.Data);
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
                var typeSchema = GuessTypeSchema(runtimeType, loadResult);

                yield return new(c, typeSchema, constantValue);
            }
        }
    }

    public static TypeSchema TryGuessTypeSchema(Type runtimeType, MarkupLoadResult loadResult)
    {
        return loadResult.ImportTables.TypeImports.FirstOrDefault(t => t.RuntimeType == runtimeType)
            ?? loadResult.ImportTables.TypeImports.FirstOrDefault(t => t.RuntimeType == runtimeType.BaseType);
    }

    public static TypeSchema GuessTypeSchema(Type runtimeType, MarkupLoadResult loadResult)
    {
        return TryGuessTypeSchema(runtimeType, loadResult)
            ?? throw new Exception($"Failed to find type schema for '{runtimeType.Name}' in {loadResult.Uri}");
    }

    private ConstantDirective EncodeConstant(object constantValue, string constantName, TypeSchema typeSchema)
    {
        var qualifiedTypeName = GetQualifiedName(typeSchema);

        // ILayout has a number of derived classes with special named instances,
        // but aren't implemented as canonical instances.
        if (constantValue is Layout.ILayout constantLayout && Layout.PredefinedLayouts.TryConvertToString(constantLayout, out var constantLayoutString))
        {
            qualifiedTypeName = GetQualifiedName(UIXTypes.MapIDToType(UIXTypeID.Layout));
            return new StringEncodedConstantDirective(constantName, qualifiedTypeName, constantLayoutString);
        }

        // Instance may be canonical and can be encoded using its name
        if (constantValue is IHasCanonicalInstances maybeCanonicalValue)
        {
            // Attempt to get the canonical name for this instance.
            // If there isn't one, then fall back to other methods.
            var canonicalName = maybeCanonicalValue.GetCanonicalName();
            if (canonicalName is not null)
                return new CanonicalInstanceConstantDirective(constantName, qualifiedTypeName, canonicalName);
        }

        // Type supports canonical instances
        if (typeSchema.SupportsCanonicalInstance && constantValue is string maybeCanonicalName)
        {
            // Attempt to get the canonical instance with this name.
            // If there isn't one, then fall back to the other methods.
            var canonicalInstance = typeSchema.FindCanonicalInstance(maybeCanonicalName);
            if (canonicalInstance is not null)
                return new CanonicalInstanceConstantDirective(constantName, qualifiedTypeName, maybeCanonicalName);
        }
        
        // Instance is explicitly encodable as a string
        if (constantValue is IStringEncodable encodable)
        {
            var encodedValue = encodable.EncodeString();
            return new StringEncodedConstantDirective(constantName, qualifiedTypeName, encodedValue);
        }

        // Type supports parsing from string, implicitly encodable as a string
        if (typeSchema.SupportsTypeConversion(_stringTypeSchema))
        {
            var encodedValue = constantValue.ToString();
            return new StringEncodedConstantDirective(constantName, qualifiedTypeName, encodedValue);
        }

        // Type supports encoding as raw bytes
        if (typeSchema.SupportsBinaryEncoding)
        {
            ByteCodeWriter writer = new();
            typeSchema.EncodeBinary(writer, constantValue);

            var reader = writer.CreateReader();
            byte[] encodedBytes = new byte[reader.Size];
            Marshal.Copy(reader.GetAddress(0), encodedBytes, 0, (int)reader.Size);
            reader.Dispose(null);

            return new BinaryEncodedConstantDirective(constantName, qualifiedTypeName, encodedBytes);
        }

        throw new NotSupportedException($"Unable to encode constant value '{constantValue}' of type '{qualifiedTypeName}'");
    }

    public record RawConstantInfo(int Index, TypeSchema Type, object Value)
    {
        public string GenerateDefaultName() => $"const{Index:D}";
    }
}

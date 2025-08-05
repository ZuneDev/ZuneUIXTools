using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

internal class DecompileContext
{
    private readonly MarkupLoadResult _loadResult;
    private readonly MarkupLoadResult _dataTableLoadResult;
    private readonly Dictionary<(ulong, uint), SyntaxTree> _scriptMap;
    private readonly Dictionary<string, XNamespace> _namespaces;
    private readonly HashSet<string> _usedNamespacePrefixes;
    private readonly Dictionary<string, string> _uriAliasMap;
    private Instruction[] _instructions;
    private Disassembler.RawConstantInfo[] _constants;

    public DecompileContext(MarkupLoadResult loadResult, MarkupLoadResult dataTableLoadResult = null)
    {
        _loadResult = loadResult;

        _dataTableLoadResult = dataTableLoadResult
            ?? loadResult.BinaryDataTable?.SharedDependenciesTableWithBinaryDataTable?.FirstOrDefault() as MarkupLoadResult;

        _uriAliasMap = new()
        {
            [_loadResult.Uri] = "me",
            ["http://schemas.microsoft.com/2007/uix"] = null
        };

        _namespaces = new()
        {
            ["me"] = "Me",
            [""] = "http://schemas.microsoft.com/2007/uix"
        };

        _usedNamespacePrefixes = [];

        _loadResult.FullLoad();
        if (_loadResult.Status == LoadResultStatus.Error)
            throw new Exception($"Failed to load '{_loadResult.ErrorContextUri}'");

        if (UseSharedDataTable)
        {
            _dataTableLoadResult.FullLoad();
            if (_dataTableLoadResult.Status == LoadResultStatus.Error)
                throw new Exception($"Failed to load '{_dataTableLoadResult.ErrorContextUri}'");
        }

        GenerateNamespaces();

        _scriptMap = [];

        _instructions = ObjectSection.Decode(_loadResult.ObjectSection)
            .OfType<Instruction>()
            .ToArray();

        _constants = Disassembler.EnumerateConstantInfo(_loadResult).ToArray();
    }

    public Instruction[] Instructions => _instructions;

    public MarkupLoadResult LoadResult => _loadResult;

    public MarkupImportTables ImportTables => _loadResult.ImportTables;

    [MemberNotNullWhen(true, nameof(_dataTableLoadResult))]
    private bool UseSharedDataTable => _dataTableLoadResult is not null;

    public Disassembler.RawConstantInfo GetConstant(Operand op) => _constants[((OperandReference)op).Index];

    public TypeSchema GetImportedType(Operand op) => ImportTables.TypeImports[(ushort)op.Value];

    public TypeSchema GetImportedType(QualifiedTypeName typeName)
    {
        var uri = MapPrefixToNamespace(typeName.NamespacePrefix);
        return ImportTables.TypeImports
            .Where(t =>  t.Name == typeName.TypeName || t.AlternateName == typeName.TypeName)
            .OrderBy(t => t.Owner.Uri == uri ? 0 : 1)
            .First();
    }

    public MethodSchema GetImportedMethod(Operand op) => ImportTables.MethodImports[(ushort)op.Value];

    public PropertySchema GetImportedProperty(Operand op) => ImportTables.PropertyImports[(ushort)op.Value];

    public ConstructorSchema GetImportedConstructor(Operand op) => ImportTables.ConstructorImports[(ushort)op.Value];

    public IEnumerable<Instruction> GetMethodBody(uint startOffset)
    {
        foreach (var instruction in Instructions.SkipWhile(i => i.Offset < startOffset))
        {
            yield return instruction;
            if (instruction.OpCode is OpCode.ReturnValue or OpCode.ReturnVoid)
                yield break;
        }
    }

    public void SetScriptContent(TypeSchema type, uint startOffset, SyntaxTree tree) => _scriptMap[(type.UniqueId, startOffset)] = tree;

    public SyntaxTree GetScriptContent(TypeSchema type, uint startOffset) => _scriptMap[(type.UniqueId, startOffset)];

    public bool TryGetScriptContent(TypeSchema type, uint startOffset, [NotNullWhen(true)] out SyntaxTree tree)
    {
        if (_scriptMap.TryGetValue((type.UniqueId, startOffset), out tree))
            return true;

        tree = null;
        return false;
    }

    public IEnumerable<SyntaxTree> GetScriptContents(TypeSchema type) => _scriptMap.Where(k => k.Key.Item1 == type.UniqueId).Select(k => k.Value);

    public IEnumerable<KeyValuePair<string, XNamespace>> GetUsedNamespaces()
    {
        return _namespaces
            .Where(p => _usedNamespacePrefixes.Contains(p.Key) && !string.IsNullOrEmpty(p.Key));
    }

    public string MapNamespaceToPrefix(string uri)
    {
        _uriAliasMap.TryGetValue(uri, out string prefix);
        _usedNamespacePrefixes.Add(prefix);
        return prefix;
    }

    public string MapPrefixToNamespace(string prefix)
    {
        return _uriAliasMap.First(kvp => kvp.Value == prefix).Key;
    }

    public QualifiedTypeName GetQualifiedName(TypeSchema schema)
    {
        var prefix = MapNamespaceToPrefix(schema.Owner.Uri);
        return new(prefix, schema.Name);
    }

    public XName GetXName(TypeSchema schema)
    {
        var prefix = MapNamespaceToPrefix(schema.Owner.Uri);
        var ns = _namespaces[prefix ?? ""];

        // Strip potential generic type parameters
        var name = schema.Name;
        var genericIndex = name.IndexOf('`');
        if (genericIndex > 0)
            name = name[..genericIndex];

        return ns + name;
    }

    private void GenerateNamespaces()
    {
        foreach (var typeImport in _loadResult.ImportTables.TypeImports)
        {
            var typeName = typeImport.Name;
            var uri = typeImport.Owner.Uri;
            if (!_uriAliasMap.TryGetValue(uri, out var namespacePrefix))
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
                if (!_namespaces.ContainsValue(uri))
                {
                    // Prevent similar imports from generating the same prefix
                    int duplicateCount = 0;
                    while (_namespaces.ContainsKey(namespacePrefix))
                        namespacePrefix = $"{baseNamespacePrefix}{++duplicateCount}";

                    _namespaces.Add(namespacePrefix, uri);
                }

                if (!_uriAliasMap.ContainsKey(uri))
                    _uriAliasMap.Add(uri, namespacePrefix);

                // Ensure that the original, un-normalized URI is saved too
                if (!_uriAliasMap.ContainsKey(ownerUri))
                    _uriAliasMap.Add(ownerUri, namespacePrefix);
            }
        }
    }
}

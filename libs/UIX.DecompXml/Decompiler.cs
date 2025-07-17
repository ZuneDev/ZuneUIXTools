using Humanizer;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

public class Decompiler
{
    private readonly MarkupLoadResult _loadResult;
    private readonly MarkupLoadResult _dataTableLoadResult;
    private readonly Dictionary<string, XNamespace> _namespaces;
    private readonly Dictionary<string, string> _uriAliasMap;
    private Instruction[] _instructions;
    private Disassembler.RawConstantInfo[] _constants;

    private Decompiler(MarkupLoadResult loadResult, MarkupLoadResult dataTableLoadResult = null)
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
    }

    public static Decompiler Load(LoadResult loadResult, LoadResult dataTableLoadResult = null)
    {
        if (loadResult is not MarkupLoadResult markupLoadResult)
            throw new ArgumentException($"Disassembly can only be performed on markup. Expected '{nameof(MarkupLoadResult)}', got '{loadResult?.GetType().Name}'.", nameof(loadResult));

        if (dataTableLoadResult is MarkupLoadResult dataTableMarkupLoadResult)
            return new(markupLoadResult, dataTableMarkupLoadResult);

        if (dataTableLoadResult is not null)
            throw new ArgumentException($"Data table must be markup. Expected '{nameof(MarkupLoadResult)}', got '{dataTableLoadResult?.GetType().Name}'.", nameof(dataTableLoadResult));

        return new(markupLoadResult);
    }

    public XDocument Decompile()
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

        GetNamespaces();

        _instructions = ObjectSection.Decode(_loadResult.ObjectSection)
            .OfType<Instruction>()
            .ToArray();

        _constants = Disassembler.EnumerateConstantInfo(_loadResult).ToArray();

        XNamespace nsUix = XNamespace.Get("http://schemas.microsoft.com/2007/uix");
        XElement xRoot = new(nsUix + "UIX", new XAttribute("xmlns", nsUix));

        foreach (var export in _loadResult.ExportTable.Cast<MarkupTypeSchema>())
        {
            var name = export.Name;
            
            var baseType = export.MarkupTypeBase;
            var baseTypeName = GetQualifiedName(baseType);

            XElement xExport = new(nsUix + export.MarkupType.ToString(),
                new XAttribute("Name", name),
                new XAttribute("Base", baseTypeName));

            var initPropsOffset = export.InitializePropertiesOffset;
            var initPropsBody = _instructions
                .SkipWhile(i => i.Offset < initPropsOffset)
                .OrderBy(i => i.Offset)
                .TakeWhile(i => i.OpCode is not (OpCode.ReturnValue or OpCode.ReturnVoid))
                .OrderBy(i => i.Offset)
                .ToArray();

            var propertyElements = new XElement[export.Properties.Length];

            Stack<XNode> xStack = new([xExport]);
            Stack<Disassembler.RawConstantInfo> constantsStack = new();

            for (int i = 0; i < initPropsBody.Length; i++)
            {
                var instruction = initPropsBody[i];

                if (instruction.OpCode is OpCode.JumpIfDictionaryContains)
                {
                }
                else if (instruction.OpCode is OpCode.PushConstant)
                {
                    var constantOperand = (OperandReference)instruction.Operands.First();
                    var constant = _constants[constantOperand.Index];
                    constantsStack.Push(constant);
                    //xStack.Push(IntoXNode(constant.Value));
                }
                else if (instruction.OpCode is OpCode.PushNull)
                {
                    constantsStack.Push(null);
                    //xStack.Push(IntoXNode(null));
                }
                else if (instruction.OpCode is OpCode.PropertyDictionaryAdd)
                {
                    var targetProperty = _loadResult.ImportTables.PropertyImports[(ushort)instruction.Operands.ElementAt(0).Value];

                    var keyReference = (OperandReference)instruction.Operands.ElementAt(1);
                    var key = _constants[keyReference.Index].Value.ToString();

                    //var xValue = xStack.Pop();
                    var value = constantsStack.Pop();

                    var targetInstance = xStack.Peek() as XElement;
                    var xDictionary = GetOrCreateElement(targetInstance, nsUix + targetProperty.Name);

                    XElement xDictionaryEntry = new(GetXName(value.Type));
                    xDictionaryEntry.SetAttributeValue("Name", key);
                    xDictionary.Add(xDictionaryEntry);

                    if (value.Value is IStringEncodable valStrEnc)
                    {
                        xDictionaryEntry.SetAttributeValue(value.Type.Name, valStrEnc);
                    }
                    else
                    {
                        xDictionaryEntry.SetAttributeValue(value.Type.Name, value.Value.ToString());
                    }
                }
            }

            xRoot.Add(xExport);
        }

        XDocument xDoc = new(xRoot);

        // Add all namespaces to root element
        foreach (var pair in _namespaces)
        {
            var prefix = pair.Key;
            var ns = pair.Value;

            if (string.IsNullOrEmpty(prefix))
                continue;

            xDoc.Root.SetAttributeValue(XNamespace.Xmlns + prefix, ns);
        }
        return xDoc;
    }

    public string DecompileToSource()
    {
        var xmlDoc = Decompile();

        XmlWriterSettings writerSettings = new()
        {
            Indent = true,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
        };

        StringBuilder sb = new();
        using (XmlWriter writer = XmlWriter.Create(sb, writerSettings))
        {
            xmlDoc.WriteTo(writer);
        }
        return sb.ToString();
    }

    private void GetNamespaces()
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
        };
    }

    private QualifiedTypeName GetQualifiedName(TypeSchema schema)
    {
        _uriAliasMap.TryGetValue(schema.Owner.Uri, out string prefix);
        return new(prefix, schema.Name);
    }

    private XName GetXName(TypeSchema schema)
    {
        _uriAliasMap.TryGetValue(schema.Owner.Uri, out string prefix);
        var ns = _namespaces[prefix ?? ""];
        return ns + schema.Name; 
    }

    private XElement GetOrCreateElement(XElement parent, XName name)
    {
        var elem = parent.Element(name);

        if (elem is null)
        {
            elem = new XElement(name);
            parent.Add(elem);
        }

        return elem;
    }

    private XNode IntoXNode(object obj)
    {
        if (obj is null)
            return new XText("{null}");

        return obj switch
        {
            string str => new XText(str),
            IStringEncodable strEnc => new XText(strEnc.EncodeString()),

            _ => throw new InvalidOperationException($"Cannot convert type '{obj.GetType().Name}' to an XNode")
        };
    }

    [MemberNotNullWhen(true, nameof(_dataTableLoadResult))]
    private bool UseSharedDataTable => _dataTableLoadResult is not null;
}

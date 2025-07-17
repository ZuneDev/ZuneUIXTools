using Humanizer;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.DecompXml.Mock;
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
    internal readonly MarkupLoadResult _loadResult;
    private readonly MarkupLoadResult _dataTableLoadResult;
    private readonly XNamespace _nsUix = XNamespace.Get("http://schemas.microsoft.com/2007/uix");
    private readonly Dictionary<string, XNamespace> _namespaces;
    private readonly HashSet<string> _usedNamespacePrefixes;
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

        _usedNamespacePrefixes = [];
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

        XElement xRoot = new(_nsUix + "UIX", new XAttribute("xmlns", _nsUix));

        foreach (var export in _loadResult.ExportTable.Cast<MarkupTypeSchema>())
        {
            var name = export.Name;
            
            var baseType = export.MarkupTypeBase;
            var baseTypeName = GetQualifiedName(baseType);

            XElement xExport = new(_nsUix + export.MarkupType.ToString(),
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

            Stack<object> stack = new([xExport]);

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
                    stack.Push(constant);
                }
                else if (instruction.OpCode is OpCode.PushNull)
                {
                    stack.Push(null);
                }
                else if (instruction.OpCode is OpCode.ConstructObject)
                {
                    var type = _loadResult.ImportTables.TypeImports[(ushort)instruction.Operands.ElementAt(0).Value];
                    var xObj = new XElement(GetXName(type));
                    stack.Push(new IrisObject(xObj, type));
                }
                else if (instruction.OpCode is OpCode.MethodInvokeStatic)
                {
                    var method = _loadResult.ImportTables.MethodImports[(ushort)instruction.Operands.First().Value];

                    int parameterCount = method.ParameterTypes.Length;
                    object[] parameters = new object[parameterCount];
                    for (parameterCount--; parameterCount >= 0; parameterCount--)
                        parameters[parameterCount] = stack.Pop();

                    var callExpression = new IrisMethodCallExpression(method, null, parameters.Select(IrisExpression.ToExpression));

                    stack.Push(callExpression);
                }
                else if (instruction.OpCode is OpCode.PropertyInitialize)
                {
                    var property = _loadResult.ImportTables.PropertyImports[(ushort)instruction.Operands.ElementAt(0).Value];
                    var value = stack.Pop();

                    var target = stack.Pop();
                    var xTarget = (XElement)ToXmlFriendlyObject(target);

                    PropertyAssignOnXElement(xTarget, property, IrisObject.Create(value, property.PropertyType, this));

                    stack.Push(new IrisObject(xTarget, property.Owner));
                }
                else if (instruction.OpCode is OpCode.PropertyDictionaryAdd)
                {
                    var targetProperty = _loadResult.ImportTables.PropertyImports[(ushort)instruction.Operands.ElementAt(0).Value];

                    var keyReference = (OperandReference)instruction.Operands.ElementAt(1);
                    var key = _constants[keyReference.Index].Value.ToString();

                    var value = stack.Pop();

                    var targetInstance = stack.Peek() as XElement;

                    PropertyDictionaryAddOnXElement(targetInstance, targetProperty, IrisObject.Create(value, null, this), key);
                }
            }

            xRoot.Add(xExport);
        }

        XDocument xDoc = new(xRoot);

        // Add all namespaces to root element
        var xNamespaceDeclarations = _namespaces
            .Where(p => _usedNamespacePrefixes.Contains(p.Key) && !string.IsNullOrEmpty(p.Key))
            .Select(p => new XAttribute(XNamespace.Xmlns + p.Key, p.Value));

        xDoc.Root.Add(xNamespaceDeclarations.ToArray());

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

    internal string MapNamespaceToPrefix(string uri)
    {
        _uriAliasMap.TryGetValue(uri, out string prefix);
        _usedNamespacePrefixes.Add(prefix);
        return prefix;
    }

    internal QualifiedTypeName GetQualifiedName(TypeSchema schema)
    {
        var prefix = MapNamespaceToPrefix(schema.Owner.Uri);
        return new(prefix, schema.Name);
    }

    private XName GetXName(TypeSchema schema)
    {
        var prefix = MapNamespaceToPrefix(schema.Owner.Uri);
        var ns = _namespaces[prefix ?? ""];
        return ns + schema.Name; 
    }

    private static XElement GetOrCreateElement(XElement parent, XName name)
    {
        var elem = parent.Element(name);

        if (elem is null)
        {
            elem = new XElement(name);
            parent.Add(elem);
        }

        return elem;
    }

    private object ToXmlFriendlyObject(object obj)
    {
        if (obj is Disassembler.RawConstantInfo rci)
            obj = rci.Value;
        else if (obj is IrisObject irisObj)
            obj = irisObj.Object;

        return obj switch
        {
            string str => str,
            IStringEncodable strEnc => strEnc.EncodeString(),
            null => "{null}",
            IrisExpression expr => '{' + expr.Decompile(this) + '}',

            XElement xElem => xElem,

            _ => throw new InvalidOperationException($"Cannot convert type '{obj.GetType().Name}' to an XML object")
        };
    }

    private XObject PropertyAssignOnXElement(XElement xTarget, PropertySchema property, IrisObject value)
    {
        object xfValue = ToXmlFriendlyObject(value.Object);

        XObject xObject;

        switch (xfValue)
        {
            case XElement xValue:
                var xProperty = GetOrCreateElement(xTarget, property.Name);
                xProperty.Add(xValue);
                xObject = xProperty;
                break;

            case string strValue:
                xObject = new XAttribute(property.Name, strValue);
                break;

            default:
                throw new InvalidOperationException();
        }

        xTarget.Add(xObject);
        return xObject;
    }

    private XElement PropertyDictionaryAddOnXElement(XElement xTarget, PropertySchema property, IrisObject value, string key)
    {
        var xDictionary = GetOrCreateElement(xTarget, _nsUix + property.Name);
        return PropertyDictionaryAddOnXElement(xDictionary, value, key);
    }

    private XElement PropertyDictionaryAddOnXElement(XElement xDictionary, IrisObject value, string key)
    {
        object xValue = ToXmlFriendlyObject(value.Object);

        XElement xDictionaryEntry;

        switch (xValue)
        {
            case string strValue:
                xDictionaryEntry = new(GetXName(value.Type));
                xDictionaryEntry.SetAttributeValue(value.Type.Name, strValue);
                break;
            case XElement xValueELem:
                xDictionaryEntry = xValueELem;
                break;
            default:
                throw new InvalidOperationException();
        }

        xDictionaryEntry.SetAttributeValue("Name", key);
        xDictionary.Add(xDictionaryEntry);

        return xDictionaryEntry;
    }

    [MemberNotNullWhen(true, nameof(_dataTableLoadResult))]
    private bool UseSharedDataTable => _dataTableLoadResult is not null;
}

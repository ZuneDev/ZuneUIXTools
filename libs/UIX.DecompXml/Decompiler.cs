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
    private readonly MarkupLoadResult _loadResult;
    private readonly MarkupLoadResult _dataTableLoadResult;
    private readonly Dictionary<string, XNamespace> _namespaces;
    private readonly Dictionary<string, string> _uriAliasMap;
    private Instruction[] _instructions;

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

            var initPropOffset = export.InitializePropertiesOffset;

            var defaultType = export.ConstructDefault();
            var initializedType = export.ConstructDefault();
            try
            {
                export.InitializeInstance(ref initializedType);
            }
            catch { }

            var propertyElements = new XElement[export.Properties.Length];
            for (int i = 0; i < export.Properties.Length; i++)
            {
                PropertySchema property = export.Properties[i];
                var typeName = GetXName(property.PropertyType);

                // TODO: Decode object section to get default property assignments
                XElement xProperty = new(typeName,
                    new XAttribute("Name", property.Name));

                var defaultValue = property.GetValue(defaultType);
                var initializedValue = property.GetValue(initializedType);
                if (defaultValue != initializedValue)
                    xProperty.Add(new XAttribute("TODO_DEFAULT", initializedValue));

                propertyElements[i] = xProperty;
            }

            XElement xProperties = new(nsUix + "Properties", propertyElements);
            xExport.Add(xProperties);

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

    [MemberNotNullWhen(true, nameof(_dataTableLoadResult))]
    private bool UseSharedDataTable => _dataTableLoadResult is not null;
}

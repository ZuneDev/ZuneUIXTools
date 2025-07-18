using Microsoft.Iris.Asm;
using Microsoft.Iris.DecompXml.Mock;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

public partial class Decompiler
{
    private static readonly XNamespace _nsUix = XNamespace.Get("http://schemas.microsoft.com/2007/uix");
    private readonly DecompileContext _context;

    private Decompiler(DecompileContext context)
    {
        _context = context;
    }

    public static Decompiler Load(LoadResult loadResult, LoadResult dataTableLoadResult = null)
    {
        if (loadResult is not MarkupLoadResult markupLoadResult)
            throw new ArgumentException($"Disassembly can only be performed on markup. Expected '{nameof(MarkupLoadResult)}', got '{loadResult?.GetType().Name}'.", nameof(loadResult));

        if (dataTableLoadResult is not MarkupLoadResult and not null)
            throw new ArgumentException($"Data table must be markup. Expected '{nameof(MarkupLoadResult)}', got '{dataTableLoadResult?.GetType().Name}'.", nameof(dataTableLoadResult));
        
        DecompileContext context = new(markupLoadResult, (MarkupLoadResult)dataTableLoadResult);
        return new(context);
    }

    public XDocument Decompile()
    {
        XElement xRoot = new(_nsUix + "UIX", new XAttribute("xmlns", _nsUix));

        foreach (var export in _context.LoadResult.ExportTable.Cast<MarkupTypeSchema>())
        {
            var name = export.Name;
            
            var baseType = export.MarkupTypeBase;
            var baseTypeName = _context.GetQualifiedName(baseType);

            XElement xExport = new(_nsUix + export.MarkupType.ToString(),
                new XAttribute("Name", name),
                new XAttribute("Base", baseTypeName));

            if (export.InitializePropertiesOffset is not uint.MaxValue)
                AnalyzeMethodForInit(export.InitializePropertiesOffset, xExport, export, name + "_prop");

            if (export.InitialEvaluateOffsets is { Length: > 0 })
            {
                XElement xScripts = new(_nsUix + "Scripts");

                foreach (var offset in export.InitialEvaluateOffsets)
                {
                    var syntaxTree = DecompileScript(offset, export);
                    var scriptText = FormatScript(syntaxTree);

                    XElement xScript = new(_nsUix + "Script", scriptText);
                    xScripts.Add(xScript);
                }

                xExport.Add(xScripts);
            }

            if (export.InitializeContentOffset is not uint.MaxValue)
                AnalyzeMethodForInit(export.InitializeContentOffset, xExport, export, name + "_cont");

            xRoot.Add(xExport);
        }

        XDocument xDoc = new(xRoot);

        // Add all namespaces to root element
        var xNamespaceDeclarations = _context.GetUsedNamespaces()
            .Select(p => new XAttribute(XNamespace.Xmlns + p.Key, p.Value))
            .ToArray();

        xDoc.Root.Add(xNamespaceDeclarations);

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

    private Stack<object> AnalyzeMethodForInit(uint startOffset, XElement elemToInit, MarkupTypeSchema initType, string methodName = "")
    {
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        Stack<object> stack = new([elemToInit]);

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

            try
            {
                switch (instruction.OpCode)
                {
                    case OpCode.PushConstant:
                        var constant = _context.GetConstant(instruction.Operands.First());
                        stack.Push(constant);
                        break;

                    case OpCode.PushNull:
                        stack.Push(null);
                        break;

                    case OpCode.ConstructObject:
                        var typeToCtor = _context.GetImportedType(instruction.Operands.ElementAt(0));
                        var xObj = new XElement(_context.GetXName(typeToCtor));
                        stack.Push(new IrisObject(xObj, typeToCtor));
                        break;

                    case OpCode.LookupSymbol:
                        var symbolIndex = (ushort)instruction.Operands.ElementAt(0).Value;
                        var symbol = initType.SymbolReferenceTable[symbolIndex];
                        stack.Push(symbol);
                        break;

                    case OpCode.MethodInvokeStatic:
                        var method = _context.GetImportedMethod(instruction.Operands.First());

                        int parameterCount = method.ParameterTypes.Length;
                        object[] parameters = new object[parameterCount];
                        for (parameterCount--; parameterCount >= 0; parameterCount--)
                            parameters[parameterCount] = stack.Pop();

                        var callExpression = new IrisMethodCallExpression(method, null, parameters.Select(IrisExpression.Wrap));

                        stack.Push(callExpression);
                        break;

                    case OpCode.PropertyGet:
                    case OpCode.PropertyGetPeek:
                    case OpCode.PropertyGetStatic:
                        var propToGet = _context.GetImportedProperty(instruction.Operands.ElementAt(0));

                        var propTarget = instruction.OpCode switch
                        {
                            OpCode.PropertyGet => IrisExpression.Wrap(stack.Pop()),
                            OpCode.PropertyGetPeek => IrisExpression.Wrap(stack.Peek()),
                            _ => null,
                        };

                        var propertyGetExpression = new IrisPropertyExpression(propToGet, propTarget);
                        stack.Push(propertyGetExpression);
                        break;

                    case OpCode.PropertyInitialize:
                        var propertyToInit = _context.GetImportedProperty(instruction.Operands.ElementAt(0));
                        var newPropValue = stack.Pop();

                        var target = stack.Pop();
                        var xTarget = (XElement)ToXmlFriendlyObject(target);

                        PropertyAssignOnXElement(xTarget, propertyToInit, IrisObject.Create(newPropValue, propertyToInit.PropertyType, _context));

                        stack.Push(new IrisObject(xTarget, propertyToInit.Owner));
                        break;

                    case OpCode.PropertyDictionaryAdd:
                        var targetDictProperty = _context.GetImportedProperty(instruction.Operands.ElementAt(0));

                        var keyReference = instruction.Operands.ElementAt(1);
                        var key = _context.GetConstant(keyReference).Value.ToString();

                        var dictValue = stack.Pop();

                        var targetInstance = (XElement)stack.Peek();

                        PropertyDictionaryAddOnXElement(targetInstance, targetDictProperty, IrisObject.Create(dictValue, null, _context), key);
                        break;

                    case OpCode.PropertyListAdd:
                        var targetListProperty = _context.GetImportedProperty(instruction.Operands.ElementAt(0));
                        var valueToAdd = stack.Pop();
                        var targetInstance2 = (XElement)ToXmlFriendlyObject(stack.Peek());

                        PropertyListAddOnXElement(targetInstance2, targetListProperty, IrisObject.Create(valueToAdd, null, _context));
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to analyze instruction `{instruction}` @ 0x{instruction.Offset:X}, {methodName}[{i}]", ex);
            }
        }

        return stack;
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

    private object ToXmlFriendlyObject(object obj, TypeSchema type = null)
    {
        if (obj is Disassembler.RawConstantInfo rci)
        {
            obj = rci.Value;
        }
        else if (obj is IrisObject irisObj)
        {
            obj = irisObj.Object;
        }

        return obj switch
        {
            string str => str,
            null => "{null}",
            bool b => b ? "true" : "false",
            IStringEncodable strEnc => strEnc.EncodeString(),
            IrisExpression expr => '{' + expr.Decompile(_context) + '}',
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),

            Layout.ILayout layoutObj
                when Layout.PredefinedLayouts.TryConvertToString(layoutObj, out var layoutName)
                => layoutName,

            XElement xElem => xElem,

            _ => SerializeToXml(obj)
        };
    }

    private XElement SerializeToXml(object obj)
    {
        var type = Disassembler.GuessTypeSchema(obj.GetType(), _context.LoadResult);

        XElement xObj = new(_context.GetXName(type));
        var defaultObj = type.ConstructDefault();

        foreach (var prop in type.Properties)
        {
            var defaultPropValue = prop.GetValue(defaultObj);
            var propValue = prop.GetValue(obj);

            if (propValue == defaultPropValue || propValue.Equals(defaultPropValue))
                continue;

            PropertyAssignOnXElement(xObj, prop, new(propValue, prop.PropertyType));
        }

        return xObj;
    }

    private XObject PropertyAssignOnXElement(XElement xTarget, PropertySchema property, IrisObject value)
    {
        object xfValue = ToXmlFriendlyObject(value.Object, value.Type);

        switch (xfValue)
        {
            case XElement xValue:
                var xProperty = GetOrCreateElement(xTarget, _nsUix + property.Name);
                xProperty.Add(xValue);
                return xProperty;

            case string strValue:
                var xAttr = new XAttribute(property.Name, strValue);
                xTarget.Add(xAttr);
                return xAttr;

            default:
                throw new InvalidOperationException();
        }
    }

    private XElement PropertyListAddOnXElement(XElement xTarget, PropertySchema property, IrisObject value)
    {
        var xDictionary = GetOrCreateElement(xTarget, _nsUix + property.Name);
        return PropertyListAddOnXElement(xDictionary, value);
    }

    private XElement PropertyListAddOnXElement(XElement xList, IrisObject value)
    {
        object xValue = ToXmlFriendlyObject(value.Object, value.Type);

        XElement xListEntry;

        switch (xValue)
        {
            case string strValue:
                xListEntry = new(_context.GetXName(value.Type));
                xListEntry.SetAttributeValue(value.Type.Name, strValue);
                break;
            case XElement xValueELem:
                xListEntry = xValueELem;
                break;
            default:
                throw new InvalidOperationException();
        }

        xList.Add(xListEntry);

        return xListEntry;
    }

    private XElement PropertyDictionaryAddOnXElement(XElement xTarget, PropertySchema property, IrisObject value, string key)
    {
        var xDictionary = GetOrCreateElement(xTarget, _nsUix + property.Name);
        return PropertyDictionaryAddOnXElement(xDictionary, value, key);
    }

    private XElement PropertyDictionaryAddOnXElement(XElement xDictionary, IrisObject value, string key)
    {
        var xDictionaryEntry = PropertyListAddOnXElement(xDictionary, value);
        xDictionaryEntry.SetAttributeValue("Name", key);
        return xDictionaryEntry;
    }
}

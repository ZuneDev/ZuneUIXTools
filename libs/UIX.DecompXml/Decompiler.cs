using Microsoft.Iris.Asm;
using Microsoft.Iris.DecompXml.Mock;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Iris.DecompXml;

public class Decompiler
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

            AnalyzeMethodForInit(export.InitializePropertiesOffset, xExport);

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

    private Stack<object> AnalyzeMethodForInit(uint startOffset, XElement elemToInit)
    {
        var methodBody = _context.GetMethodBody(startOffset);

        Stack<object> stack = new([elemToInit]);

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

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
                    var type = _context.GetImportedType(instruction.Operands.ElementAt(0));
                    var xObj = new XElement(_context.GetXName(type));
                    stack.Push(new IrisObject(xObj, type));
                    break;

                case OpCode.MethodInvokeStatic:
                    var method = _context.GetImportedMethod(instruction.Operands.First());

                    int parameterCount = method.ParameterTypes.Length;
                    object[] parameters = new object[parameterCount];
                    for (parameterCount--; parameterCount >= 0; parameterCount--)
                        parameters[parameterCount] = stack.Pop();

                    var callExpression = new IrisMethodCallExpression(method, null, parameters.Select(IrisExpression.ToExpression));

                    stack.Push(callExpression);
                    break;

                case OpCode.PropertyInitialize:
                    var property = _context.GetImportedProperty(instruction.Operands.ElementAt(0));
                    var propValue = stack.Pop();

                    var target = stack.Pop();
                    var xTarget = (XElement)ToXmlFriendlyObject(target);

                    PropertyAssignOnXElement(xTarget, property, IrisObject.Create(propValue, property.PropertyType, _context));

                    stack.Push(new IrisObject(xTarget, property.Owner));
                    break;

                case OpCode.PropertyDictionaryAdd:
                    var targetProperty = _context.GetImportedProperty(instruction.Operands.ElementAt(0));

                    var keyReference = instruction.Operands.ElementAt(1);
                    var key = _context.GetConstant(keyReference).Value.ToString();

                    var dictValue = stack.Pop();

                    var targetInstance = stack.Peek() as XElement;

                    PropertyDictionaryAddOnXElement(targetInstance, targetProperty, IrisObject.Create(dictValue, null, _context), key);
                    break;
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
            IrisExpression expr => '{' + expr.Decompile(_context) + '}',

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
                xDictionaryEntry = new(_context.GetXName(value.Type));
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
}

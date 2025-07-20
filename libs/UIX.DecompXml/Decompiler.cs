using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm;
using Microsoft.Iris.Debug.Data;
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

            XElement xExport = new(_nsUix + export.MarkupType.ToString(),
                new XAttribute("Name", name));
            
            var baseType = export.MarkupTypeBase;
            if (baseType is not null)
            {
                var baseTypeName = _context.GetQualifiedName(baseType);
                xExport.SetAttributeValue("Base", baseTypeName);
            }

            if (export is ClassTypeSchema classExport)
            {
                if (classExport.IsShared)
                    xExport.SetAttributeValue("Shared", true);
            }

            if (export.InitializePropertiesOffset is not uint.MaxValue)
                AnalyzeMethodForInit(export.InitializePropertiesOffset, xExport, export, name + "_prop");

            if (export.InitializeLocalsInputOffset is not uint.MaxValue)
                AnalyzeMethodForInit(export.InitializeLocalsInputOffset, xExport, export, name + "_locl");

            if (export.InitialEvaluateOffsets is { Length: > 0 })
            {
                var xScripts = GetOrCreateElement(xExport, _nsUix + "Scripts");

                foreach (var offset in export.InitialEvaluateOffsets)
                {
                    var syntaxTree = DecompileScript(offset, export);
                    var scriptText = FormatScript(syntaxTree);

                    XElement xScript = new(_nsUix + "Script", scriptText);
                    xScripts.Add(xScript);
                }
            }

            if (export.RefreshGroupOffsets is { Length: > 0 })
            {
                var xScripts = GetOrCreateElement(xExport, _nsUix + "Scripts");
                
                foreach (var offset in export.RefreshGroupOffsets)
                {
                    var scriptText = AnalyzeRefreshMethod(offset, export, $"{name}_rfsh_0x{offset:X}");

                    XElement xScript = new(_nsUix + "Script", scriptText);
                    xScripts.Add(xScript);
                }
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
            Encoding = Encoding.UTF8,
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

                    case OpCode.PushThis:
                        stack.Push(elemToInit);
                        break;

                    case OpCode.ConstructObject:
                        var typeToCtor = _context.GetImportedType(instruction.Operands.ElementAt(0));
                        var xObj = new XElement(_context.GetXName(typeToCtor));
                        stack.Push(new IrisObject(xObj, typeToCtor));
                        break;

                    case OpCode.LookupSymbol:
                        var symbolIndex = (ushort)instruction.Operands.ElementAt(0).Value;
                        stack.Push(initType.SymbolReferenceTable[symbolIndex]);
                        break;

                    case OpCode.PropertyInitialize:
                        var propertyToInit = _context.GetImportedProperty(instruction.Operands.ElementAt(0));
                        var newPropValue = stack.Pop();

                        var target = stack.Pop();
                        var xTarget = ToXmlFriendlyObject(target) as XElement;
                        if (xTarget is null)
                        {

                        }

                        PropertyAssignOnXElement(xTarget, propertyToInit, IrisObject.Create(newPropValue, propertyToInit.PropertyType, _context));

                        stack.Push(new IrisObject(xTarget, propertyToInit.Owner));
                        break;

                    case OpCode.PropertyDictionaryAdd:
                        var targetDictProperty = _context.GetImportedProperty(instruction.Operands.ElementAt(0));

                        var keyReference = instruction.Operands.ElementAt(1);
                        var key = _context.GetConstant(keyReference).Value.ToString();

                        var dictValue = stack.Pop();
                        var dictValueType = initType.InheritableSymbolsTable?
                            .FirstOrDefault(s => s.Name == key)?
                            .Type;

                        var targetInstance = stack.Peek() as XElement;
                        if (targetInstance is null)
                        {

                        }

                        PropertyDictionaryAddOnXElement(targetInstance, targetDictProperty, IrisObject.Create(dictValue, dictValueType, _context), key);
                        break;

                    case OpCode.PropertyListAdd:
                        var valueToAdd = stack.Pop();
                        TypeSchema valueToAddType = null;

                        if (valueToAdd is SymbolReference symRef)
                        {
                            valueToAddType = initType.InheritableSymbolsTable?
                                .FirstOrDefault(s => s.Name == symRef.Symbol)?
                                .Type;
                        }

                        var valueToAddObj = IrisObject.Create(valueToAdd, valueToAddType, _context);

                        var targetInstance2 = (XElement)ToXmlFriendlyObject(stack.Peek());

                        var targetListPropertyIndex = (ushort)instruction.Operands.First().Value;
                        if (targetListPropertyIndex != ushort.MaxValue)
                        {
                            var targetListProperty = _context.ImportTables.PropertyImports[targetListPropertyIndex];

                            if (valueToAddObj.Type is null)
                            {
                                var valueRuntimeType = targetListProperty.PropertyType.RuntimeType.GetGenericArguments().FirstOrDefault();
                                valueToAddType = _context.ImportTables.TypeImports.FirstOrDefault(t => t.RuntimeType == valueRuntimeType);
                                valueToAddObj = valueToAddObj with { Type = valueToAddType };
                            }

                            PropertyListAddOnXElement(targetInstance2, targetListProperty, valueToAddObj);
                        }
                        else
                        {
                            PropertyListAddOnXElement(targetInstance2, valueToAddObj);
                        }
                        break;

                    case OpCode.InitializeInstance:
                    case OpCode.JumpIfDictionaryContains:
                    case OpCode.ConstructListenerStorage:
                        // These instructions are inconsequential for determining how objects are initialized
                        break;

                    case OpCode.ConstructObjectParam:
                    case OpCode.MethodInvoke:
                    case OpCode.MethodInvokePeek:
                    case OpCode.MethodInvokeStatic:
                    case OpCode.MethodInvokePushLastParam:
                    case OpCode.MethodInvokeStaticPushLastParam:
                    case OpCode.PropertyGet:
                    case OpCode.PropertyGetPeek:
                    case OpCode.PropertyGetStatic:
                    case OpCode.Operation:
                        // These instructions only appear in initializers as inline expressions
                        if (!TryDecompileExpression(instruction, stack))
                            throw new NotImplementedException();
                        break;

                    case not OpCode.ReturnVoid:
                        Console.WriteLine($"Unsupported instruction: {instruction}");
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

    private string AnalyzeRefreshMethod(uint startOffset, MarkupTypeSchema initType, string methodName = "")
    {
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        Stack<object> stack = new();

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

            try
            {
                switch (instruction.OpCode)
                {
                    case OpCode.Listen:
                    case OpCode.DestructiveListen:
                        var listenerIndex = (ushort)instruction.Operands.ElementAt(0).Value;
                        var listenerType = (ListenerType)(byte)instruction.Operands.ElementAt(1).Value;
                        var watchIndex = (ushort)instruction.Operands.ElementAt(2).Value;
                        var scriptId = (uint)instruction.Operands.ElementAt(3).Value;

                        var refreshOffset = uint.MaxValue;
                        if (instruction.OpCode is OpCode.DestructiveListen)
                            refreshOffset = (uint)instruction.Operands.ElementAt(4).Value;

                        MarkupTypeSchema markupTypeSchema = initType;
                        uint num = scriptId >> 27;
                        while (num != markupTypeSchema.TypeDepth)
                            markupTypeSchema = markupTypeSchema.MarkupTypeBase;

                        var scriptOffset = scriptId & 0x07FFFFFFU;

                        string watch = null;
                        InstructionObjectSource watchSource = InstructionObjectSource.Dynamic;
                        switch (listenerType)
                        {
                            case ListenerType.Property:
                                watch = _context.ImportTables.PropertyImports[watchIndex].Name;
                                watchSource = InstructionObjectSource.PropertyImports;
                                break;
                            case ListenerType.Event:
                                watch = _context.ImportTables.EventImports[watchIndex].Name;
                                watchSource = InstructionObjectSource.EventImports;
                                break;
                            case ListenerType.Symbol:
                                watch = initType.SymbolReferenceTable[watchIndex].Symbol;
                                watchSource = InstructionObjectSource.SymbolReference;
                                break;
                        }

                        //object handlerObj = stack.Peek();
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to analyze instruction `{instruction}` @ 0x{instruction.Offset:X}, {methodName}[{i}]", ex);
            }
        }

        return "";
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
            ExpressionSyntax expr => FormatInlineExpression(expr),
            SymbolReference symRef => '{' + symRef.Symbol + '}',
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
        object xfValue = ToXmlFriendlyObject(value);

        switch (xfValue)
        {
            case XElement xValue:
                var xProperty = GetOrCreateElement(xTarget, _nsUix + property.Name);

                // Flatten collections
                if (typeof(System.Collections.IList).IsAssignableFrom(value.Type.RuntimeType)
                    || typeof(System.Collections.IDictionary).IsAssignableFrom(value.Type.RuntimeType))
                {
                    xProperty.Add(xValue.Elements());
                }
                else
                {
                    xProperty.Add(xValue);
                }

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
        var xList = GetOrCreateElement(xTarget, _nsUix + property.Name);
        return PropertyListAddOnXElement(xList, value);
    }

    private XElement PropertyListAddOnXElement(XElement xList, IrisObject value)
    {
        object xValue = ToXmlFriendlyObject(value);

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

    private XElement PropertyDictionaryAddOnXElement(XElement xDictionary, IrisObject value, string key)
    {
        var xDictionaryEntry = PropertyListAddOnXElement(xDictionary, value);
        xDictionaryEntry.SetAttributeValue("Name", key);
        return xDictionaryEntry;
    }

    private XElement PropertyDictionaryAddOnXElement(XElement xTarget, PropertySchema property, IrisObject value, string key)
    {
        var xDictionary = GetOrCreateElement(xTarget, _nsUix + property.Name);
        return PropertyDictionaryAddOnXElement(xDictionary, value, key);
    }
}

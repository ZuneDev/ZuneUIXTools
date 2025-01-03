﻿using Microsoft.Iris.Asm.Models;
using Sprache;
using System;
using System.Globalization;
using System.Linq;

namespace Microsoft.Iris.Asm;

partial class Lexer
{
    private static IResult<IDirective> ParseDirective(IInput input)
    {
        input = ConsumeWhitespace(input);
        var line = input.Line;
        var column = input.Column;

        var directiveBeginResult = Parse.Char('.')(input);
        input = directiveBeginResult.Remainder;
        if (!directiveBeginResult.WasSuccessful)
            return Result.Failure<IDirective>(input, "Invalid directive", ["Expected '.', followed by an identifier"]);

        var directiveIdResult = WordText(input);
        input = directiveIdResult.Remainder;
        if (!directiveIdResult.WasSuccessful)
            return Result.Failure<IDirective>(input, "Invalid directive", ["Expected a valid directive name"]);

        IDirective directive;
        switch (directiveIdResult.Value.ToUpperInvariant())
        {
            case "IMPORT":
                return ParseImportAsDirective(input);

            case "SECTION":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid section directive", ["Expected a section name"]);

                var sectionNameResult = Parse.Letter.AtLeastOnce().Token().Text()(input);
                input = sectionNameResult.Remainder;
                if (!sectionNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid section name", ["Expected a section name containing only letters"]);

                directive = new SectionDirective(sectionNameResult.Value)
                {
                    Line = line,
                    Column = column,
                };
                break;

            case "CONSTANT":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected a consant declaration"]);

                var constNameResult = Identifier.Token()(input);
                input = constNameResult.Remainder;
                if (!constNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected a name for the constant"]);

                input = Parse.Char('=').Token()(input).Remainder;

                // Types that can't be encoded as simple strings, such as FlowLayout,
                // are defined inline using XML elements.
                bool isInlineXml = Parse.Char('<')(input).WasSuccessful;
                if (isInlineXml)
                {
                    // Find the end of the XML tag
                    var xmlPartResult = Parse.AnyChar.Until(Parse.String("/>")).Text()(input);
                    input = xmlPartResult.Remainder;
                    if (!xmlPartResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected valid XML"]);

                    var xmlElem = System.Xml.Linq.XElement.Parse($"{xmlPartResult.Value}/>");
                    var xmlPrefix = xmlElem.Name.NamespaceName == string.Empty
                        ? null : xmlElem.Name.NamespaceName;

                    QualifiedTypeName typeName = new(xmlPrefix, xmlElem.Name.LocalName);
                    var content = xmlElem.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

                    directive = new ConstantDirective(constNameResult.Value, typeName, content)
                    {
                        Line = line,
                    };
                }
                else
                {
                    var typeNameResult = QualifiedTypeName.Token()(input);
                    input = typeNameResult.Remainder;
                    if (!typeNameResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected qualified name of type to construct"]);

                    var encodingMarkerResult = Parse.Char('.')(input);
                    input = encodingMarkerResult.Remainder;
                    if (!encodingMarkerResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected '.', followed by the persist mode."]);

                    var persistModeResult = Parse.CharExcept('(').AtLeastOnce().Text().Token()(input);
                    input = persistModeResult.Remainder;
                    if (!persistModeResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["No persist mode was specified."]);

                    Markup.MarkupConstantPersistMode? persistMode = persistModeResult.Value.ToLowerInvariant() switch
                    {
                        "bin" => Markup.MarkupConstantPersistMode.Binary,
                        "str" => Markup.MarkupConstantPersistMode.FromString,
                        "can" => Markup.MarkupConstantPersistMode.Canonical,
                        _ => null
                    };

                    var openBracketResult = Parse.Char('(').Token()(input);
                    input = openBracketResult.Remainder;
                    if (!openBracketResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected '('"]);

                    var contentResult = Parse.CharExcept(')').AtLeastOnce().Text().Token()(input);
                    input = contentResult.Remainder;
                    if (!contentResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected constant value"]);

                    var closeBracketResult = Parse.Char(')').Token()(input);
                    input = closeBracketResult.Remainder;
                    if (!closeBracketResult.WasSuccessful)
                        return Result.Failure<IDirective>(input, "Invalid constant directive", ["Expected ')'"]);

                    var constantName = constNameResult.Value;
                    var typeName = typeNameResult.Value;

                    if (persistMode == Markup.MarkupConstantPersistMode.FromString)
                    {
                        directive = new StringEncodedConstantDirective(constantName, typeName, contentResult.Value)
                        {
                            Line = line,
                            Column = column,
                        };
                    }
                    else if (persistMode == Markup.MarkupConstantPersistMode.Canonical)
                    {
                        directive = new CanonicalInstanceConstantDirective(constantName, typeName, contentResult.Value)
                        {
                            Line = line,
                            Column = column,
                        };
                    }
                    else if (persistMode == Markup.MarkupConstantPersistMode.Binary)
                    {
                        byte[] constantBytes;
                        var byteParts = contentResult.Value.Split(',');
                        if (byteParts.Length == 1)
                        {
                            var constantStr = byteParts[0].Trim();
                            if (constantStr.StartsWith("0x"))
                            {
                                constantStr = constantStr[2..];
                            }

                            if (constantStr.Length == 2)
                            {
                                constantBytes = [byte.Parse(constantStr, NumberStyles.HexNumber)];
                            }
                            else if (constantStr.Length == 4)
                            {
                                constantBytes = BitConverter.GetBytes(
                                    ushort.Parse(constantStr, NumberStyles.HexNumber));
                            }
                            else if (constantStr.Length == 8)
                            {
                                constantBytes = BitConverter.GetBytes(
                                    uint.Parse(constantStr, NumberStyles.HexNumber));
                            }
                            else
                            {
                                return Result.Failure<IDirective>(input, "Invalid constant directive", [$"Expected a 1, 2, or 4 hex number, or a list of bytes."]);
                            }
                        }
                        else
                        {
                            constantBytes = byteParts
                                .Select(s => byte.Parse(s.Trim().TrimStart('0', 'x'), NumberStyles.HexNumber))
                                .ToArray();
                        }

                        directive = new BinaryEncodedConstantDirective(constantName, typeName, constantBytes)
                        {
                            Line = line,
                            Column = column,
                        };
                    }
                    else
                    {
                        return Result.Failure<IDirective>(input, "Invalid constant directive", [$"'{persistModeResult.Value}' is not a valid persist mode."]);
                    }
                }
                break;

            case "EXPORT":
                if (StatementEnd(input).WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected export information"]);

                var labelPrefixResult = Identifier.Token()(input);
                input = labelPrefixResult.Remainder;
                if (!labelPrefixResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected prefix of labels to export"]);

                var listenerCountResult = WholeNumber.Token()(input);
                input = listenerCountResult.Remainder;
                if (!listenerCountResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected listener count"]);

                if (!uint.TryParse(listenerCountResult.Value, out var listenerCount))
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected export listener count to be an unsigned integer"]);

                var baseTypeNameResult = AlphanumericText.Token()(input);
                input = baseTypeNameResult.Remainder;
                if (!baseTypeNameResult.WasSuccessful)
                    return Result.Failure<IDirective>(input, "Invalid export directive", ["Expected base type name"]);

                var labelPrefix = labelPrefixResult.Value;
                var baseTypeName = baseTypeNameResult.Value;
                directive = new ExportDirective(labelPrefix, listenerCount, baseTypeName)
                {
                    Line = line,
                    Column = column,
                };
                break;

            default:
                return Result.Failure<IDirective>(input, $"Unknown import type '{directiveIdResult.Value}'", ["Expected 'export', 'import', or 'section'"]);
        }

        return Result.Success(directive, input);
    }
}

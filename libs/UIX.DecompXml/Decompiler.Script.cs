using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.DecompXml.Mock;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Markup.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml;

partial class Decompiler
{
    private SyntaxTree DecompileScript(uint startOffset, MarkupTypeSchema export)
    {
        var methodBody = _context.GetMethodBody(startOffset);

        Stack<BlockSyntax> blockStack = [];
        blockStack.Push(Block());

        Stack<object> stack = new();

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];
            var opCode = instruction.OpCode;

            try
            {
                switch (opCode)
                {
                    case OpCode.PushConstant:
                        var constant = _context.GetConstant(instruction.Operands.First());
                        stack.Push(IrisExpression.ToSyntax(constant, _context));
                        break;

                    case OpCode.PushNull:
                        stack.Push(LiteralExpression(SyntaxKind.NullLiteralExpression));
                        break;

                    case OpCode.DiscardValue:
                        stack.Pop();
                        break;

                    case OpCode.ConstructObject:
                        var typeToCtor = _context.GetImportedType(instruction.Operands.First());
                        stack.Push(ObjectCreationExpression(
                            IrisExpression.ToSyntax(typeToCtor, _context),
                            ArgumentList(),
                            null
                        ));
                        break;

                    case OpCode.LookupSymbol:
                        var symbolIndex = (ushort)instruction.Operands.First().Value;
                        stack.Push(IrisExpression.ToSyntax(export.SymbolReferenceTable[symbolIndex]));
                        break;

                    case OpCode.WriteSymbol:
                    case OpCode.WriteSymbolPeek:
                        var writeSymbolIndex = (ushort)instruction.Operands.First().Value;

                        var newSymbolValue = opCode is OpCode.WriteSymbolPeek
                            ? stack.Peek() : stack.Pop();

                        var symbolAssignmentExpr = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            IrisExpression.ToSyntax(export.SymbolReferenceTable[writeSymbolIndex]),
                            IrisExpression.ToSyntax(newSymbolValue, _context)
                        );

                        AddStatementToBlock(blockStack, ExpressionStatement(symbolAssignmentExpr));
                        break;

                    case OpCode.MethodInvoke:
                    case OpCode.MethodInvokePeek:
                    case OpCode.MethodInvokeStatic:
                    case OpCode.MethodInvokePushLastParam:
                    case OpCode.MethodInvokeStaticPushLastParam:
                        var methodSchema = _context.GetImportedMethod(instruction.Operands.First());

                        int parameterCount = methodSchema.ParameterTypes.Length;
                        var parameters = new ArgumentSyntax[parameterCount];
                        for (parameterCount--; parameterCount >= 0; parameterCount--)
                        {
                            var parameter = IrisExpression.ToSyntax(stack.Pop(), _context);
                            parameters[parameterCount] = Argument(parameter);
                        }

                        bool isStatic = opCode is OpCode.MethodInvokeStatic or OpCode.MethodInvokeStaticPushLastParam;
                        bool peek = opCode is OpCode.MethodInvokePeek;
                        bool pushLastParam = opCode is OpCode.MethodInvokePushLastParam or OpCode.MethodInvokeStaticPushLastParam;

                        var targetObj = opCode switch
                        {
                            OpCode.MethodInvokeStatic or
                            OpCode.MethodInvokeStaticPushLastParam => methodSchema.Owner,
                            _ when peek => stack.Peek(),
                            _ => stack.Pop(),
                        };

                        var methodExpression = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IrisExpression.ToSyntax(targetObj, _context),
                            IdentifierName(methodSchema.Name)
                        );

                        var methodResult = InvocationExpression(methodExpression, ArgumentList([.. parameters]));

                        if (methodSchema.ReturnType != VoidSchema.Type)
                        {
                            stack.Push(methodResult);
                        }
                        else
                        {
                            AddStatementToBlock(blockStack, ExpressionStatement(methodResult));
                        }

                        if (pushLastParam)
                        {
                            stack.Push(parameters[^1].Expression);
                        }
                        break;

                    case OpCode.PropertyGet:
                    case OpCode.PropertyGetPeek:
                    case OpCode.PropertyGetStatic:
                        var propToGet = _context.GetImportedProperty(instruction.Operands.First());

                        var propTarget = instruction.OpCode switch
                        {
                            OpCode.PropertyGet => stack.Pop(),
                            OpCode.PropertyGetPeek => stack.Peek(),
                            _ => propToGet.Owner,
                        };

                        var propertyGetExpression = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IrisExpression.ToSyntax(propTarget, _context),
                            IdentifierName(propToGet.Name)
                        );

                        stack.Push(propertyGetExpression);
                        break;

                    case OpCode.PropertyInitialize:
                        var propertyToInit = _context.GetImportedProperty(instruction.Operands.First());
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
                        var targetListProperty = _context.GetImportedProperty(instruction.Operands.First());
                        var valueToAdd = stack.Pop();
                        var targetInstance2 = (XElement)ToXmlFriendlyObject(stack.Peek());

                        PropertyListAddOnXElement(targetInstance2, targetListProperty, IrisObject.Create(valueToAdd, null, _context));
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to decompile instruction `{instruction}` @ 0x{instruction.Offset:X} in script for {export.Name}", ex);
            }
        }

        if (blockStack.Count > 1)
            throw new InvalidOperationException($"Failed to decompile script for {export.Name}, more than one top-level code block");
        else if (blockStack.Count < 0)
            throw new InvalidOperationException($"Failed to decompile script for {export.Name}, no top-level code blocks");

        // Unwrap top-most block to avoid extra curly braces around entire script
        var topBlock = blockStack.Pop();

        return SyntaxTree(
            CompilationUnit().WithMembers(
                [..topBlock.Statements.Select(GlobalStatement)]
            )
        );
    }

    public static string FormatScript(SyntaxTree tree, CancellationToken token = default)
    {
        return tree
            .GetRoot(token)
            .NormalizeWhitespace()
            .SyntaxTree
            .GetText(token)
            .ToString();
    }

    private static void AddStatementToBlock(Stack<BlockSyntax> blockStack, StatementSyntax statement)
    {
        var block = blockStack.Pop();
        block = block.AddStatements(statement);
        blockStack.Push(block);
    }
}

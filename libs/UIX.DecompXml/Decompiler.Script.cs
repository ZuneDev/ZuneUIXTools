using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm.Models;
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
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        Stack<CodeBlockInfo> blockStack = [];
        blockStack.Push(new(0, methodBody[^1].Offset, SyntaxKind.Block, null));

        Stack<object> stack = new();

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

            // TODO: Handle for loops
            if (instruction.Offset == blockStack.Peek().EndOffset)
            {
                // Make sure there is only one top-level block
                if (blockStack.Count > 1)
                {
                    var currentBlock = blockStack.Pop();
                    currentBlock.FinalizeBlock(blockStack.Peek());
                }
                else
                {
                    // End of function
                    if (i + 1 != methodBody.Length)
                        throw new InvalidOperationException("Expected end of function!");

                    break;
                }
            }
                
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

                    case OpCode.LookupSymbol:
                        var symbolIndex = (ushort)instruction.Operands.ElementAt(0).Value;
                        stack.Push(export.SymbolReferenceTable[symbolIndex]);
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

                        blockStack.Peek().Statements.Add(ExpressionStatement(symbolAssignmentExpr));
                        break;

                    case OpCode.PropertyAssign:
                    case OpCode.PropertyAssignStatic:
                        var propToSet = _context.GetImportedProperty(instruction.Operands.First());

                        var propSetTarget = opCode is OpCode.PropertyAssignStatic
                            ? propToSet.Owner
                            : stack.Pop();

                        var newPropValue = IrisExpression.ToSyntax(stack.Peek(), _context);

                        var propertySetExpression = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IrisExpression.ToSyntax(propSetTarget, _context),
                                IdentifierName(propToSet.Name)
                            ),
                            newPropValue
                        );

                        blockStack.Peek().Statements.Add(ExpressionStatement(propertySetExpression));
                        break;

                    case OpCode.PropertyDictionaryAdd:
                        // TODO
                        var targetDictProperty = _context.GetImportedProperty(instruction.Operands.ElementAt(0));

                        var keyReference = instruction.Operands.ElementAt(1);
                        var key = _context.GetConstant(keyReference).Value.ToString();

                        var dictValue = stack.Pop();

                        var targetInstance = (XElement)stack.Peek();

                        PropertyDictionaryAddOnXElement(targetInstance, targetDictProperty, IrisObject.Create(dictValue, null, _context), key);
                        break;

                    case OpCode.PropertyListAdd:
                        // TODO
                        var targetListProperty = _context.GetImportedProperty(instruction.Operands.First());
                        var valueToAdd = stack.Pop();
                        var targetInstance2 = (XElement)ToXmlFriendlyObject(stack.Peek());

                        PropertyListAddOnXElement(targetInstance2, targetListProperty, IrisObject.Create(valueToAdd, null, _context));
                        break;

                    case OpCode.JumpIfFalse:
                    case OpCode.JumpIfFalsePeek:
                    case OpCode.JumpIfTruePeek:
                        var jumpToOffset = (uint)instruction.Operands.First().Value;

                        // TODO: What about for loops?
                        if (instruction.Offset > jumpToOffset)
                        {
                            throw new NotImplementedException();
                        }

                        var isPeek = opCode is OpCode.JumpIfFalsePeek or OpCode.JumpIfTruePeek or OpCode.JumpIfNullPeek;
                        var rawJumpCondition = IrisExpression.ToSyntax(isPeek ? stack.Peek() : stack.Pop(), _context);

                        var jumpCondition = opCode switch
                        {
                            OpCode.JumpIfFalse or
                            OpCode.JumpIfFalsePeek => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, rawJumpCondition),

                            OpCode.JumpIfTruePeek => rawJumpCondition,

                            OpCode.JumpIfNullPeek => BinaryExpression(SyntaxKind.EqualsExpression,
                                rawJumpCondition,
                                LiteralExpression(SyntaxKind.NullLiteralExpression)),

                            _ => throw new NotImplementedException()
                        };

                        var ifBlock = new CodeBlockInfo(instruction.Offset, jumpToOffset, SyntaxKind.IfStatement, jumpCondition);
                        blockStack.Push(ifBlock);
                        break;

                    default:
                        if (!TryDecompileExpression(instruction, stack, blockStack))
                            throw new NotImplementedException();
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

    public static string FormatInlineExpression(ExpressionSyntax expr, CancellationToken token = default)
    {
        var exprStr = expr
            .NormalizeWhitespace()
            .SyntaxTree
            .GetText(token)
            .ToString();
        return '{' + exprStr + '}';
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

    private bool TryDecompileExpression(Instruction instruction, Stack<object> stack, Stack<CodeBlockInfo> blockStack = null)
    {
        var opCode = instruction.OpCode;

        switch (opCode)
        {
            case OpCode.PushThis:
                stack.Push(ThisExpression());
                break;

            case OpCode.ConstructObject:
            case OpCode.ConstructObjectParam:
                var typeToCtor = _context.GetImportedType(instruction.Operands.First());

                List<ArgumentSyntax> ctorParameters = [];
                if (opCode is OpCode.ConstructObjectParam)
                {
                    var ctorSchema = _context.GetImportedConstructor(instruction.Operands.ElementAt(1));

                    int ctorParameterCount = ctorSchema.ParameterTypes.Length;
                    ctorParameters.Capacity = ctorParameterCount;

                    for (ctorParameterCount--; ctorParameterCount >= 0; ctorParameterCount--)
                    {
                        var parameter = IrisExpression.ToSyntax(stack.Pop(), _context);
                        ctorParameters.Add(Argument(parameter));
                    }
                    ctorParameters.Reverse();
                }

                stack.Push(ObjectCreationExpression(
                    IrisExpression.ToSyntax(typeToCtor, _context),
                    ArgumentList([..ctorParameters]),
                    null
                ));
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
                    blockStack?.Peek().Statements.Add(ExpressionStatement(methodResult));
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

                var propGetTarget = instruction.OpCode switch
                {
                    OpCode.PropertyGet => stack.Pop(),
                    OpCode.PropertyGetPeek => stack.Peek(),
                    _ => propToGet.Owner,
                };

                var propertyGetExpression = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IrisExpression.ToSyntax(propGetTarget, _context),
                    IdentifierName(propToGet.Name)
                );

                stack.Push(propertyGetExpression);
                break;

            case OpCode.Operation:
                DecompileOperation(instruction, stack);
                break;

            default:
                return false;
        }

        return true;
    }

    private ExpressionSyntax DecompileOperation(Instruction instruction, Stack<object> stack)
    {
        var opHost = _context.GetImportedType(instruction.Operands.ElementAt(0));

        var op = (OperationType)(int)(byte)instruction.Operands.ElementAt(1).Value;
        var isUnary = TypeSchema.IsUnaryOperation(op);
        var opSyntax = OperationToSyntaxKind(op);

        ExpressionSyntax operationExpr;

        if (isUnary)
        {
            var left = IrisExpression.ToSyntax(stack.Pop(), _context);
            var isPostfix = op is OperationType.PostIncrement or OperationType.PostDecrement;

            operationExpr = isPostfix
                ? PostfixUnaryExpression(opSyntax, left)
                : PrefixUnaryExpression(opSyntax, left);
        }
        else
        {
            var right = IrisExpression.ToSyntax(stack.Pop(), _context);
            var left = IrisExpression.ToSyntax(stack.Pop(), _context);

            operationExpr = BinaryExpression(opSyntax, left, right);
        }

        stack.Push(operationExpr);
        return operationExpr;
    }

    private static SyntaxKind OperationToSyntaxKind(OperationType operation)
    {
        return operation switch
        {
            OperationType.MathAdd                       => SyntaxKind.AddExpression,
            OperationType.MathSubtract                  => SyntaxKind.SubtractExpression,
            OperationType.MathMultiply                  => SyntaxKind.MultiplyExpression,
            OperationType.MathDivide                    => SyntaxKind.DivideExpression,
            OperationType.MathModulus                   => SyntaxKind.ModuloExpression,
            OperationType.MathNegate                    => SyntaxKind.UnaryMinusExpression,

            OperationType.LogicalAnd                    => SyntaxKind.LogicalAndExpression,
            OperationType.LogicalOr                     => SyntaxKind.LogicalOrExpression,
            OperationType.LogicalNot                    => SyntaxKind.LogicalNotExpression,

            OperationType.RelationalEquals              => SyntaxKind.EqualsExpression,
            OperationType.RelationalNotEquals           => SyntaxKind.NotEqualsExpression,
            OperationType.RelationalLessThan            => SyntaxKind.LessThanExpression,
            OperationType.RelationalGreaterThan         => SyntaxKind.GreaterThanExpression,
            OperationType.RelationalLessThanEquals      => SyntaxKind.LessThanOrEqualExpression,
            OperationType.RelationalGreaterThanEquals   => SyntaxKind.GreaterThanOrEqualExpression,
            OperationType.RelationalIs                  => SyntaxKind.IsExpression,

            OperationType.PostIncrement                 => SyntaxKind.PostIncrementExpression,
            OperationType.PostDecrement                 => SyntaxKind.PostDecrementExpression,

            _ => throw new ArgumentException($"Invalid operation type '{operation}'", nameof(operation))
        };
    }
}

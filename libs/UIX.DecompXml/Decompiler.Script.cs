using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.DecompXml.Mock;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Markup.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml;

partial class Decompiler
{
    private static readonly TypeSchema _listType = UIXTypes.MapIDToType(UIXTypeID.List);
    private static readonly TypeSchema _dictionaryType = UIXTypes.MapIDToType(UIXTypeID.Dictionary);

    private SyntaxTree DecompileScript(uint startOffset, MarkupTypeSchema export, string? attributeName = null)
    {
        var statements = DecompileMethod(startOffset, export, attributeName);
        return CreateTree(statements);
    }

    public List<StatementSyntax> DecompileMethod(uint startOffset, MarkupTypeSchema export, string? attributeName = null)
    {
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        var controlBlocks = ControlFlowAnalyzer.CreateGraph(methodBody);
        var dotGraph = ControlFlowAnalyzer.SerializeToGraphviz(controlBlocks);
        Console.WriteLine(dotGraph);

        Stack<CodeBlockInfo> blockStack = [];
        blockStack.Push(new(0, methodBody[^1].Offset, SyntaxKind.Block, null));

        HashSet<string> scopedLocals = [];
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
                        var value = stack.Pop();
                        if (value is ExpressionSyntax expr)
                        {
                            var lastStatement = blockStack.Peek().Statements[^1];

                            if (!lastStatement.DescendantNodes().Any(n => n.IsEquivalentTo(expr)))
                                blockStack.Peek().Statements.Add(ExpressionStatement(expr));
                        }
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

                        var symbolRef = export.SymbolReferenceTable[writeSymbolIndex];
                        var symbolIdentifierExpr = IrisExpression.ToSyntax(symbolRef);
                        var newSymbolValueExpr = SimplifyExpression(IrisExpression.ToSyntax(newSymbolValue, _context), true);

                        StatementSyntax symbolWriteExpr;

                        if (symbolRef.Origin is SymbolOrigin.ScopedLocal && !scopedLocals.Contains(symbolRef.Symbol))
                        {
                            // Scoped locals need to be declared the first time they're assigned
                            var newSymbolIrisObj = IrisObject.Create(newSymbolValue, null, _context);
                            var typeSchema = newSymbolIrisObj.Type ?? UIXTypes.MapIDToType(UIXTypeID.Object);

                            symbolWriteExpr = LocalDeclarationStatement(VariableDeclaration(
                                IrisExpression.ToSyntax(typeSchema, _context),
                                SingletonSeparatedList(
                                    VariableDeclarator(symbolRef.Symbol)
                                        .WithInitializer(EqualsValueClause(newSymbolValueExpr))
                                )
                            ));
                        }
                        else
                        {
                            var symbolAssignmentExpr = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                symbolIdentifierExpr,
                                newSymbolValueExpr
                            );
                            symbolWriteExpr = ExpressionStatement(symbolAssignmentExpr);
                        }

                        blockStack.Peek().Statements.Add(symbolWriteExpr);
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

                    case OpCode.VerifyTypeCast:
                        var objToCast = stack.Pop();
                        var typeToCastTo = _context.GetImportedType(instruction.Operands.First());

                        stack.Push(Parenthesize(CastExpression(
                            IrisExpression.ToSyntax(typeToCastTo, _context),
                            IrisExpression.ToSyntax(objToCast, _context)
                        )));
                        break;

                    case OpCode.JumpIfFalse:
                    case OpCode.JumpIfFalsePeek:
                    case OpCode.JumpIfTruePeek:
                        var jumpToOffset = (uint)instruction.Operands.First().Value;

                        // TODO: What about for loops?
                        if (instruction.Offset > jumpToOffset)
                        {
                        }

                        var isPeek = opCode is OpCode.JumpIfFalsePeek or OpCode.JumpIfTruePeek or OpCode.JumpIfNullPeek;
                        var rawJumpCondition = IrisExpression.ToSyntax(isPeek ? stack.Peek() : stack.Pop(), _context);

                        // TODO: Invert jump condition when decompiling to blocks instead of gotos
                        var jumpCondition = opCode switch
                        {
                            OpCode.JumpIfFalse or
                            OpCode.JumpIfFalsePeek => LogicalNotOf(rawJumpCondition),

                            OpCode.JumpIfTruePeek => rawJumpCondition,

                            OpCode.JumpIfNullPeek => BinaryExpression(SyntaxKind.EqualsEqualsToken,
                                rawJumpCondition,
                                LiteralExpression(SyntaxKind.NullLiteralExpression)),

                            _ => throw new NotImplementedException()
                        };

                        //var ifBlock = new CodeBlockInfo(instruction.Offset, jumpToOffset, SyntaxKind.IfStatement, jumpCondition);
                        //blockStack.Push(ifBlock);

                        var ifBlock = IfStatement(SimplifyExpression(jumpCondition),
                            GotoStatement(SyntaxKind.GotoStatement, IdentifierName($"UIB_{jumpToOffset:X4}")))
                            .WithLeadingTrivia(Comment($"/* UIB_{instruction.Offset:X4} */"));
                        blockStack.Peek().Statements.Add(ifBlock);

                        break;

                    case OpCode.Jump:
                        var jumpOffset = (uint)instruction.Operands.First().Value;

                        // TODO
                        blockStack.Peek().Statements.Add(GotoStatement(SyntaxKind.GotoStatement, IdentifierName($"UIB_{jumpOffset:X4}")));
                        break;

                    case OpCode.ReturnValue:
                        var returnStatement = ReturnStatement(IrisExpression.ToSyntax(stack.Pop(), _context));
                        blockStack.Peek().Statements.Add(returnStatement);
                        break;

                    case OpCode.ReturnVoid:
                        // Include return statement when we're not in the main block (which would return anyway)
                        // or when we're not at the end of the function
                        if (blockStack.Count > 1 || i + 1 < methodBody.Length)
                            blockStack.Peek().Statements.Add(ReturnStatement());
                        break;

                    case OpCode.ClearSymbol:
                        // Ignore these instructions
                        break;

                    default:
                        if (!TryDecompileExpression(instruction, stack, blockStack))
                        {
                            var unsupportedComment = Comment($"// Unsupported instruction: {instruction}");
                            blockStack.Peek().Statements.Add(EmptyStatement().WithLeadingTrivia(unsupportedComment));
                        }
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
        var statements = blockStack.Pop().Statements;

        if (attributeName is not null)
        {
            var scriptAttribute = Attribute(IdentifierName(attributeName));
            statements[0] = statements[0]
                .WithAttributeLists(SingletonList(AttributeList([scriptAttribute])));
        }

        return statements;
    }

    private MethodDeclarationSyntax DecompileMethodDeclaration(MarkupMethodSchema method, MarkupTypeSchema export)
    {
        var targetType = export.ResolveScriptId(method.CodeOffset, out var offset);
        var methodBody = DecompileMethod(offset, export);

        var parameters = method.ParameterTypes
            .Zip(method.ParameterNames, (t, n) => Parameter(Identifier(n)).WithType(IrisExpression.ToSyntax(t, _context)));

        var modifiers = new SyntaxTokenList();
        
        // See ValidateMethod for handling of virtual and override keywords
        if (method.IsVirtual)
            modifiers.Add(Token(SyntaxKind.VirtualKeyword));
        else if (((MarkupTypeSchema)method.Owner).VirtualMethods.Contains(method))
            modifiers.Add(Token(SyntaxKind.OverrideKeyword));

        var methodDeclaration = MethodDeclaration(
            IrisExpression.ToSyntax(method.ReturnType, _context),
            method.Name
        );

        return methodDeclaration
            .WithParameterList(ParameterList([..parameters]))
            .WithBody(Block(methodBody))
            .WithModifiers(modifiers);
    }

    private void AnalyzeRefreshMethod(uint startOffset, MarkupTypeSchema initType, string methodName = "")
    {
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        Stack<object> stack = new();
        stack.Push(initType);

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

            try
            {
                switch (instruction.OpCode)
                {
                    case OpCode.LookupSymbol:
                        var symbolIndex = (ushort)instruction.Operands.ElementAt(0).Value;
                        stack.Push(initType.SymbolReferenceTable[symbolIndex]);
                        break;

                    case OpCode.Listen:
                    case OpCode.DestructiveListen:
                        var listenerIndex = (ushort)instruction.Operands.ElementAt(0).Value;
                        var listenerType = (ListenerType)(byte)instruction.Operands.ElementAt(1).Value;
                        var watchIndex = (ushort)instruction.Operands.ElementAt(2).Value;
                        var scriptId = (uint)instruction.Operands.ElementAt(3).Value;

                        var refreshOffset = uint.MaxValue;
                        if (instruction.OpCode is OpCode.DestructiveListen)
                            refreshOffset = (uint)instruction.Operands.ElementAt(4).Value;

                        string watch = null;
                        switch (listenerType)
                        {
                            case ListenerType.Property:
                                watch = _context.ImportTables.PropertyImports[watchIndex].Name;
                                break;

                            case ListenerType.Event:
                                watch = _context.ImportTables.EventImports[watchIndex].Name;
                                break;

                            case ListenerType.Symbol:
                                watch = initType.SymbolReferenceTable[watchIndex].Symbol;
                                break;
                        }

                        // What does this mean?
                        if (scriptId is uint.MaxValue)
                            break;

                        object handlerObj = stack.Peek();

                        try
                        {
                            var markupTypeSchema = initType.ResolveScriptId(scriptId, out var scriptOffset);
                            var scriptContent = _context.GetScriptContent(initType, scriptOffset);
                            var scriptRoot = scriptContent.GetRoot();

                            var nodesDbg = scriptRoot.DescendantNodes()
                                .OfType<MemberAccessExpressionSyntax>()
                                .Select(m => $"{m.Expression}.{m.Name}")
                                .ToArray();

                            SyntaxNode node = scriptRoot
                                .DescendantNodes()
                                .OfType<MemberAccessExpressionSyntax>()
                                .FirstOrDefault(n => n.Expression.ToString() == $"{handlerObj}" && n.Name.ToString() == watch);

                            if (node is not null)
                            {
                                var octothorpeTrivia = SkippedTokensTrivia()
                                    .AddTokens(BadToken(TriviaList(), "#", TriviaList()));

                                SyntaxNode newNode = node
                                    .WithLeadingTrivia(TriviaList(Trivia(octothorpeTrivia)))
                                    .WithTrailingTrivia(TriviaList(Trivia(octothorpeTrivia)));

                                SyntaxNode? parent = node.Parent;
                                while (parent is not null)
                                {
                                    newNode = node.Parent.ReplaceNode(node, newNode);
                                    node = node.Parent;
                                    parent = node.Parent;
                                }

                                _context.SetScriptContent(initType, scriptOffset, newNode.SyntaxTree);
                            }
                        }
                        catch { }

                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to analyze instruction `{instruction}` @ 0x{instruction.Offset:X}, {methodName}[{i}]", ex);
            }
        }
    }

    public static SyntaxTree CreateTree(IEnumerable<StatementSyntax> statements)
    {
        return SyntaxTree(
            CompilationUnit().WithMembers(
                [.. statements.Select(GlobalStatement)]
            )
        );
    }

    public static string FormatInlineExpression(ExpressionSyntax expr, CancellationToken token = default) => '{' + FormatSyntaxNode(expr, token) + '}';

    public static string FormatScript(SyntaxTree tree, CancellationToken token = default) => FormatSyntaxNode(tree.GetRoot(token), token);

    public static string FormatScript(IEnumerable<StatementSyntax> statements, CancellationToken token = default) => FormatScript(CreateTree(statements), token);

    public static string FormatSyntaxNode(SyntaxNode root, CancellationToken token = default)
    {
        return root
            .NormalizeWhitespace()
            .SyntaxTree
            .GetText(token)
            .ToString();
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

                var methodTargetExpression = IrisExpression.ToSyntax(targetObj, _context);

                ExpressionSyntax methodResult;
                if ((methodSchema.Owner == _listType || methodSchema.Owner == _dictionaryType) && methodSchema.Name == "get_Item")
                {
                    // Use brackets for list and dictionary indexing
                    methodResult = ElementAccessExpression(methodTargetExpression, BracketedArgumentList([.. parameters]));
                }
                else
                {
                    var methodExpression = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        methodTargetExpression,
                        IdentifierName(methodSchema.Name)
                    );
                    methodResult = InvocationExpression(methodExpression, ArgumentList([.. parameters]));
                }

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

            case OpCode.IsCheck:
                var typeToCheckFor = _context.GetImportedType(instruction.Operands.First());
                var objToCheck = stack.Pop();

                stack.Push(IsPatternExpression(
                    IrisExpression.ToSyntax(objToCheck, _context),
                    TypePattern(IrisExpression.ToSyntax(typeToCheckFor, _context))
                ));
                break;

            case OpCode.TypeOf:
                var typeOfSchema = _context.GetImportedType(instruction.Operands.First());
                var typeOfExpr = TypeOfExpression(IrisExpression.ToSyntax(typeOfSchema, _context));
                stack.Push(typeOfExpr);
                break;

            case OpCode.ConvertType:
                var destinationTypeSchema = _context.GetImportedType(instruction.Operands.First());
                var typeCastExpr = CastExpression(
                    IrisExpression.ToSyntax(destinationTypeSchema, _context),
                    Parenthesize(IrisExpression.ToSyntax(stack.Pop(), _context))
                );
                stack.Push(Parenthesize(typeCastExpr));
                break;

            default:
                return false;
        }

        return true;
    }

    private ExpressionSyntax DecompileOperation(Instruction instruction, Stack<object> stack)
    {
        var op = instruction.OperationType.Value;
        var isUnary = TypeSchema.IsUnaryOperation(op);
        var opSyntax = OperationToSyntaxKind(op);

        ExpressionSyntax operationExpr;

        if (isUnary)
        {
            var left = Parenthesize(IrisExpression.ToSyntax(stack.Pop(), _context));
            var isPostfix = op is OperationType.PostIncrement or OperationType.PostDecrement;

            operationExpr = isPostfix
                ? PostfixUnaryExpression(opSyntax, left)
                : PrefixUnaryExpression(opSyntax, left);
        }
        else
        {
            var right = IrisExpression.ToSyntax(stack.Pop(), _context);
            var left = IrisExpression.ToSyntax(stack.Pop(), _context);

            operationExpr = BinaryExpression(opSyntax,
                ParenthesizedExpression(left),
                ParenthesizedExpression(right)
            );
        }

        operationExpr = SimplifyExpression(operationExpr);
        stack.Push(operationExpr);
        return operationExpr;
    }

    private static ExpressionSyntax LogicalNotOf(ExpressionSyntax originalExpression)
    {
        ExpressionSyntax negatedExpression = null;
        var innerExpression = originalExpression;

        if (originalExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
            innerExpression = parenthesizedExpression.Expression;

        if (innerExpression is IsPatternExpressionSyntax isPatternExpression)
        {
            var baseTypePattern = isPatternExpression.Pattern;
            negatedExpression = isPatternExpression.WithPattern(UnaryPattern(baseTypePattern));
        }

        if (innerExpression is BinaryExpressionSyntax binaryExpression)
        {
            var notOperatorToken = SyntaxKind.None;
            var operatorToken = binaryExpression.OperatorToken.Kind();

            switch (operatorToken)
            {
                case SyntaxKind.EqualsEqualsToken:
                    notOperatorToken = SyntaxKind.ExclamationEqualsToken;
                    break;

                case SyntaxKind.ExclamationEqualsToken:
                    notOperatorToken = SyntaxKind.EqualsEqualsToken;
                    break;

                case SyntaxKind.LessThanToken:
                    notOperatorToken = SyntaxKind.GreaterThanEqualsToken;
                    break;

                case SyntaxKind.GreaterThanToken:
                    notOperatorToken = SyntaxKind.LessThanEqualsToken;
                    break;

                case SyntaxKind.LessThanEqualsToken:
                    notOperatorToken = SyntaxKind.GreaterThanToken;
                    break;

                case SyntaxKind.GreaterThanEqualsToken:
                    notOperatorToken = SyntaxKind.LessThanToken;
                    break;

                case SyntaxKind.AmpersandAmpersandToken:
                case SyntaxKind.BarBarToken:
                    // Apply De Morgan's laws
                    var invertedOperatorToken = operatorToken is SyntaxKind.AmpersandAmpersandToken
                        ? SyntaxKind.LogicalOrExpression : SyntaxKind.LogicalAndExpression;
                    negatedExpression = BinaryExpression(invertedOperatorToken,
                        LogicalNotOf(binaryExpression.Left),
                        LogicalNotOf(binaryExpression.Right));
                    break;
            }

            if (negatedExpression is null)
            {
                if (notOperatorToken is SyntaxKind.None)
                    throw new Exception($"Cannot take logical not of expression:\r\n{originalExpression}");
                negatedExpression = binaryExpression.WithOperatorToken(Token(notOperatorToken));
            }
        }

        negatedExpression ??= PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parenthesize(originalExpression));

        if (originalExpression is ParenthesizedExpressionSyntax)
            negatedExpression = Parenthesize(negatedExpression);

        return negatedExpression;

        //dynamic zuneUI = "";
        //dynamic configuration = "";
        //if ((zuneUI.ZuneShell.DefaultInstance.CurrentPage is QuickplayPage)
        //    || ((zuneUI.ZuneShell.DefaultInstance.CurrentPage is StartupPage) && string.IsNullOrEmpty(zuneUI.Shell.SessionStartupPath) && (configuration.ClientConfiguration.Shell.StartupPage == zuneUI.Shell.MainFrame.Quickplay.DefaultUIPath))
        //    || ((zuneUI.ZuneShell.DefaultInstance.CurrentPage is StartupPage) && (zuneUI.Shell.SessionStartupPath == zuneUI.Shell.MainFrame.Quickplay.DefaultUIPath)))
        //{

        //}
    }

    private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)
    {
        //if (expression is LiteralExpressionSyntax or IdentifierNameSyntax)
        //    return expression;

        return ParenthesizedExpression(expression);
    }

    private static ExpressionSyntax SimplifyExpression(ExpressionSyntax expression, bool canRemoveParentheses = false)
    {
        if (expression is PrefixUnaryExpressionSyntax prefixedExpression)
        {
            expression = prefixedExpression.WithOperand(SimplifyExpression(prefixedExpression.Operand, true));
        }
        else if (expression is PostfixUnaryExpressionSyntax postfixedExpression)
        {
            expression = postfixedExpression.WithOperand(SimplifyExpression(postfixedExpression.Operand, true));
        }
        else if (expression is AssignmentExpressionSyntax assignmentExpression)
        {
            expression = assignmentExpression.WithRight(SimplifyExpression(assignmentExpression.Right, true));
        }
        else if (expression is BinaryExpressionSyntax binaryExpression)
        {
            var left = SimplifyExpression(binaryExpression.Left, true);
            var right = SimplifyExpression(binaryExpression.Right, true);

            var leftBinaryExpr = left as BinaryExpressionSyntax;
            var rightBinaryExpr = right as BinaryExpressionSyntax;
            if (leftBinaryExpr is not null && rightBinaryExpr is not null)
            {
                if (!leftBinaryExpr.IsKind(rightBinaryExpr.Kind()))
                {
                    left = Parenthesize(left);
                    right = Parenthesize(right);
                }
            }

            expression = binaryExpression
                .WithLeft(left)
                .WithRight(right);
        }
        else if (expression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            var innerExpression = parenthesizedExpression.Expression;
            canRemoveParentheses |= ExpressionNeverRequiresParenthesis(innerExpression);
        }

        if (canRemoveParentheses && expression is ParenthesizedExpressionSyntax pExpr)
            return pExpr.Expression;
        return expression;
    }

    private static bool ExpressionNeverRequiresParenthesis(ExpressionSyntax expr)
    {
        return expr is LiteralExpressionSyntax or IdentifierNameSyntax or ParenthesizedExpressionSyntax or InvocationExpressionSyntax
            or IsPatternExpressionSyntax;
    }

    private record QuickplayPage;
    private record StartupPage;

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

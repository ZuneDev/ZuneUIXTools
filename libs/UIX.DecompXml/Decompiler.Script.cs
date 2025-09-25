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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Iris.DecompXml;

partial class Decompiler
{
    private static readonly TypeSchema _listType = UIXTypes.MapIDToType(UIXTypeID.List);
    private static readonly TypeSchema _dictionaryType = UIXTypes.MapIDToType(UIXTypeID.Dictionary);

    private SyntaxTree DecompileScript(uint startOffset, MarkupTypeSchema export)
    {
        var statements = DecompileMethod(startOffset, export);
        return CreateTree(statements);
    }

    public List<StatementSyntax> DecompileMethod(uint startOffset, MarkupTypeSchema export)
    {
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        var cfa = new ControlFlowAnalyzer(methodBody);
        var controlBlocks = cfa.ControlBlocks;
        var dotGraph = cfa.SerializeToGraphviz();
        Console.WriteLine(dotGraph);

        Stack<CodeBlockInfo> blockStack = [];
        blockStack.Push(new(0, methodBody[^1].Offset));

        HashSet<uint> jumpFalseToOffsets = new(methodBody
            .Where(i => i.OpCode is OpCode.JumpIfFalse)
            .Select(i => (uint)i.Operands.First().Value));

        HashSet<uint> jumpToOffsets = new(methodBody
            .Where(i => i.OpCode is OpCode.Jump)
            .Select(i => (uint)i.Operands.First().Value));

        HashSet<uint> foreachLoopHeadOffsets = [];

        Dictionary<string, TypeSchema> scopedLocals = [];
        Stack<object> stack = new();

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

            while (blockStack.Count > 1)
            {
                var currentBlock = blockStack.Pop();

                if (currentBlock.EndOffset != instruction.Offset)
                {
                    blockStack.Push(currentBlock);
                    break;
                }

                System.Diagnostics.Debug.WriteLine($"END: Finalizing {currentBlock}");
                currentBlock.FinalizeBlock(blockStack.Peek());
            }

            //if (jumpToOffsets.Contains(instruction.Offset) && TryPeekBlock<ElseBlockInfo>(out _))
            //{
            //    // This address marks the end of the affirmative branch of an IF clause.
            //    // If multiple blocks lead to this address, then we're outside of the IF clause entirely.
            //    // Otherwise, it's probably the start of an ELSE clause.

            //    while (blockStack.Count > 1)
            //    {
            //        var currentBlock = blockStack.Pop();

            //        if (currentBlock.AdditionalInfo is not (ElseBlockInfo))
            //            break;

            //        if (currentBlock.EndOffset is uint.MaxValue)
            //            currentBlock = currentBlock with { EndOffset = instruction.Offset };

            //        System.Diagnostics.Debug.WriteLine($"JMP: Finalizing ELSE {currentBlock}");
            //        currentBlock.FinalizeBlock(blockStack.Peek());
            //    }
            //}

            //if (!foreachLoopHeadOffsets.Contains(instruction.Offset) && cfa.IsAlwaysExecuted(instruction.Offset))
            //{
            //    while (blockStack.Count > 1)
            //    {
            //        var currentBlock = blockStack.Pop();
            //        if (currentBlock.EndOffset is uint.MaxValue)
            //        {

            //        }

            //        if (currentBlock.EndOffset != instruction.Offset || currentBlock.AdditionalInfo is not (IfBlockInfo or ElseBlockInfo))
            //            break;

            //        System.Diagnostics.Debug.WriteLine($"Always exec'ed: Automatically finalizing {currentBlock}");
            //        currentBlock.FinalizeBlock(blockStack.Peek());
            //    }
            //}

            var opCode = instruction.OpCode;

            try
            {
                switch (opCode)
                {
                    case OpCode.MethodInvoke:
                        var methodSchema = _context.GetImportedMethod(instruction.Operands.First());

                        // `Enumerator GetEnumerator()` marks the start of a foreach loop
                        if (!methodSchema.Name.Equals("GetEnumerator", StringComparison.InvariantCulture)
                            || methodSchema.IsStatic
                            || methodSchema.ParameterTypes.Length != 0
                            || methodSchema.ReturnType != UIXTypes.MapIDToType(UIXTypeID.Enumerator))
                        {
                            goto default;
                        }

                        var preheaderBlock = cfa.GetByInstruction(instruction);

                        var headOffset = preheaderBlock.NextOffset;
                        foreachLoopHeadOffsets.Add(headOffset);

                        var headBlock = cfa.GetByStartOffset(headOffset);
                        var exitOffset = ((BasicControlFlowBlock)headBlock).BranchTargetOffset;
                        var loopBodyEndOffset = methodBody
                            .Select(i => i.Offset)
                            .OrderByDescending(i => i)
                            .SkipWhile(i => i >= exitOffset)
                            .First();

                        var forEachBlockInfo = new ForEachBlockInfo
                        {
                            Source = IrisExpression.ToSyntax(stack.Pop(), _context),
                        };

                        var foreachBlock = new CodeBlockInfo(instruction.Offset, loopBodyEndOffset, forEachBlockInfo);
                        blockStack.Push(foreachBlock);

                        break;

                    case OpCode.MethodInvokePeek:
                        // Ignore MoveNext calls when in a foreach loop, as long as we haven't already initialized this loop
                        if (!TryPeekBlock<ForEachBlockInfo>(out var forEachBlockInfo1) || forEachBlockInfo1.Type is not null)
                            goto default;

                        var methodSchemaPeek = _context.GetImportedMethod(instruction.Operands.First());

                        // `bool MoveNext()` marks the start of a foreach loop
                        if (!methodSchemaPeek.Name.Equals("MoveNext", StringComparison.InvariantCulture)
                            || methodSchemaPeek.IsStatic
                            || methodSchemaPeek.ParameterTypes.Length != 0
                            || methodSchemaPeek.ReturnType != UIXTypes.MapIDToType(UIXTypeID.Boolean))
                        {
                            goto default;
                        }

                        break;

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
                            if (expr is ParenthesizedExpressionSyntax parenExpr)
                                expr = parenExpr.Expression;

                            var statements = blockStack.Peek().Statements;
                            if (statements.Count > 0)
                            {
                                var lastStatement = statements[^1];
                                if (lastStatement.DescendantNodes().Any(n => n.IsEquivalentTo(expr)))
                                    break;
                            }

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

                        if (symbolRef.Origin is SymbolOrigin.ScopedLocal && !scopedLocals.ContainsKey(symbolRef.Symbol))
                        {
                            // Scoped locals need to be declared the first time they're assigned
                            TypeSchema typeSchema = null;
                            if (newSymbolValue is SymbolReference { Origin: SymbolOrigin.ScopedLocal } newSymbolValueRef)
                            {
                                scopedLocals.TryGetValue(newSymbolValueRef.Symbol, out typeSchema);
                            }

                            var newSymbolIrisObj = IrisObject.Create(newSymbolValue, typeSchema, _context, export);
                            typeSchema = newSymbolIrisObj.Type ?? UIXTypes.MapIDToType(UIXTypeID.Object);

                            symbolWriteExpr = LocalDeclarationStatement(VariableDeclaration(
                                IrisExpression.ToSyntax(typeSchema, _context),
                                SingletonSeparatedList(
                                    VariableDeclarator(symbolRef.Symbol)
                                        .WithInitializer(EqualsValueClause(newSymbolValueExpr))
                                )
                            ));

                            scopedLocals[symbolRef.Symbol] = typeSchema;
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

                    case OpCode.PropertyGetPeek:
                        // PGETP is only used in foreach loops

                        if (!TryPeekBlock<ForEachBlockInfo>(out var forEachBlockInfo2))
                            throw new InvalidOperationException("Unexpected call to Current outside of a foreach loop");

                        var propToGet = _context.GetImportedProperty(instruction.Operands.First());

                        // `object Current` gets the item for this iteration of the loop
                        if (!propToGet.Name.Equals("Current", StringComparison.InvariantCulture)
                            || propToGet.IsStatic
                            || !propToGet.CanRead
                            || propToGet.PropertyType != UIXTypes.MapIDToType(UIXTypeID.Object))
                        {
                            throw new InvalidOperationException("Unexpected PGETP instruction in foreach loop");
                        }

                        var vtcInstruction = methodBody[++i];
                        if (vtcInstruction.OpCode is not OpCode.VerifyTypeCast)
                            throw new InvalidOperationException($"Expected a VTC instruction, got {vtcInstruction.OpCode}");

                        var loopVariableType = _context.GetImportedType(vtcInstruction.Operands.First());

                        var wsymInstruction = methodBody[++i];
                        if (wsymInstruction.OpCode is not OpCode.WriteSymbol)
                            throw new InvalidOperationException($"Expected a WSYM instruction, got {wsymInstruction.OpCode}");
                        
                        var loopVariableSymbol = export.SymbolReferenceTable[(ushort)wsymInstruction.Operands.First().Value].Symbol;

                        forEachBlockInfo2.Type = IrisExpression.ToSyntax(loopVariableType, _context);
                        forEachBlockInfo2.Identifier = loopVariableSymbol;

                        stack.Push(IdentifierName(loopVariableSymbol));

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

                        if (opCode is OpCode.JumpIfFalse && TryPeekBlock<ForEachBlockInfo>(out var jmpfForEachBlockInfo)
                            && jmpfForEachBlockInfo.Type is null)
                            break;

                        var isPeek = opCode is OpCode.JumpIfFalsePeek or OpCode.JumpIfTruePeek;
                        var jumpCondition = IrisExpression.ToSyntax(isPeek ? stack.Peek() : stack.Pop(), _context);

                        if (opCode is OpCode.JumpIfFalse)
                        {
                            // JMPF is used to evaluate the branch condition
                            var ifBlockEndOffset = methodBody
                               .Reverse()
                               .SkipWhile(i => i.Offset >= jumpToOffset)
                               .First()
                               .Offset;

                            var ifBlock = new CodeBlockInfo(instruction.Offset, ifBlockEndOffset, new IfBlockInfo(jumpCondition));
                            blockStack.Push(ifBlock);
                        }
                        else
                        {
                            // JMPFP and JMPTP are only used to implement short-circuiting
                            var ifBlock = SimplifyExpression(jumpCondition);
                            stack.Push(ifBlock);
                        }

                        break;

                    case OpCode.Jump:
                        var jumpOffset = (uint)instruction.Operands.First().Value;

                        if (jumpOffset < instruction.Offset)
                        {
                            // End of loop

                            if (foreachLoopHeadOffsets.Contains(jumpOffset))
                            {
                                //var currentBlock = blockStack.Pop() with { EndOffset = instruction.Offset };
                                //System.Diagnostics.Debug.WriteLine($"JMP: Finalizing loop {currentBlock}");
                                //currentBlock.FinalizeBlock(blockStack.Peek());
                            }
                            else
                            {
                                throw new NotImplementedException("For and while loops are not supported at this time.");
                            }
                        }
                        else
                        {
                            // End of if block, skipping else block

                            // Figure out where the else block ends by searching for the last instruction we skip
                            var elseBlockEndOffset = methodBody
                                .Reverse()
                                .SkipWhile(i => i.Offset >= jumpOffset)
                                .First()
                                .Offset;

                            //var currentBlock = blockStack.Pop() with { EndOffset = instruction.Offset };
                            //System.Diagnostics.Debug.WriteLine($"JMP: Finalizing presumed IF {currentBlock}");
                            //currentBlock.FinalizeBlock(blockStack.Peek());

                            var elseBlock = new CodeBlockInfo(instruction.Offset, elseBlockEndOffset, new ElseBlockInfo());
                            blockStack.Push(elseBlock);
                        }

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
        return blockStack.Pop().Statements;

        bool TryPeekBlock<T>([NotNullWhen(true)] out T additionalInfo) where T : ICodeBlockAdditionalInfo
        {
            if (blockStack.Count > 1)
            {
                var currentBlock = blockStack.Peek();
                if (currentBlock.AdditionalInfo is T a)
                {
                    additionalInfo = a;
                    return true;
                }
            }

            additionalInfo = default;
            return false;
        }
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

    private void AddMethodAttribute(List<StatementSyntax> statements, string attributeName)
    {
        var attribute = Attribute(IdentifierName(attributeName));
        statements[0] = statements[0]
            .AddAttributeLists(AttributeList([attribute]));
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

                    case OpCode.PropertyGet:
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

                        var markupTypeSchema = initType.ResolveScriptId(scriptId, out var scriptOffset);

                        if (!_context.TryGetScriptContent(initType, scriptOffset, out var scriptContent))
                        {
                            var statements = DecompileMethod(scriptOffset, initType);

                            scriptContent = CreateTree(statements);
                            _context.SetScriptContent(initType, scriptOffset, scriptContent);
                        }

                        var scriptRoot = scriptContent.GetRoot();

                        if (listenerType is not ListenerType.Symbol)
                        {
                            var memberAccessExpr = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IrisExpression.ToSyntax(handlerObj, _context),
                                IdentifierName(watch));

                            var attributeArgument = AttributeArgument(memberAccessExpr);
                            var attributeName = IdentifierName("DeclareTrigger");
                            
                            var firstStatement = scriptRoot.DescendantNodes().OfType<StatementSyntax>().First();

                            // Avoid adding duplicate triggers
                            var existingTriggers = firstStatement.AttributeLists
                                .SelectMany(al => al.Attributes)
                                .Where(a => a.Name.IsEquivalentTo(attributeName))
                                .SelectMany(a => a.ArgumentList.Arguments)
                                .ToList();

                            var hasDuplicateTrigger = existingTriggers
                                .Any(arg => arg.IsEquivalentTo(attributeArgument)
                                    || (arg.Expression is MemberAccessExpressionSyntax argExpr && argExpr.Expression.IsEquivalentTo(memberAccessExpr)));

                            if (!hasDuplicateTrigger)
                            {
                                // Remove redundant triggers. For example, if we're adding `Management.ScreenGraphicsSlider.ChosenValue`
                                // we don't need to trigger on `Management.ScreenGraphicsSlider`.
                                var precursorTriggers = existingTriggers
                                    .Select(argExpr =>  argExpr.Expression)
                                    .OfType<MemberAccessExpressionSyntax>()
                                    .Where(memberAccessExpr.Expression.IsEquivalentTo)
                                    .Select(e => (AttributeArgumentSyntax)e.Parent)
                                    .ToList();

                                foreach (var precursorTrigger in precursorTriggers)
                                    existingTriggers.Remove(precursorTrigger);
                                existingTriggers.Add(attributeArgument);

                                // Reconstruct existing and new attributes
                                var attributes = existingTriggers
                                    .Select(arg => Attribute(attributeName, AttributeArgumentList(SingletonSeparatedList(arg))));

                                var newFirstStatement = firstStatement
                                    .WithAttributeLists(SingletonList(AttributeList(SeparatedList(attributes))));
                                scriptRoot = RecursiveReplaceNode(firstStatement, newFirstStatement);
                            }
                        }

                        SyntaxNode node = scriptRoot
                            .DescendantNodes()
                            .OfType<MemberAccessExpressionSyntax>()
                            .Where(expr => expr.Parent is not AttributeArgumentSyntax)
                            .FirstOrDefault(n => n.Expression.ToString() == $"{handlerObj}" && n.Name.ToString() == watch);

                        if (node is not null)
                        {
                            var octothorpeTrivia = SkippedTokensTrivia()
                                .AddTokens(BadToken(TriviaList(), "#", TriviaList()));

                            SyntaxNode newNode = node
                                .WithLeadingTrivia(TriviaList(Trivia(octothorpeTrivia)))
                                .WithTrailingTrivia(TriviaList(Trivia(octothorpeTrivia)));

                            scriptRoot = RecursiveReplaceNode(node, newNode);
                        }

                        _context.SetScriptContent(initType, scriptOffset, scriptRoot.SyntaxTree);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to analyze instruction `{instruction}` @ 0x{instruction.Offset:X}, {methodName}[{i}]", ex);
            }
        }
    }

    private static SyntaxNode RecursiveReplaceNode(SyntaxNode oldNode, SyntaxNode newNode)
    {
        var parent = oldNode.Parent;
        while (parent is not null)
        {
            newNode = oldNode.Parent.ReplaceNode(oldNode, newNode);
            oldNode = oldNode.Parent;
            parent = oldNode.Parent;
        }
        return newNode;
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

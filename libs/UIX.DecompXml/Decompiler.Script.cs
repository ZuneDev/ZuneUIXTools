using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Debug.Symbols;
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

    private SyntaxTree DecompileScript(uint startOffset, MarkupTypeSchema export)
    {
        var statements = DecompileMethod(startOffset, export);
        return CreateTree(statements, startOffset);
    }

    public List<StatementSyntax> DecompileMethod(uint startOffset, MarkupTypeSchema export)
    {
        var methodBody = _context.GetMethodBody(startOffset).ToArray();

        var cfa = new ControlFlowAnalyzer(methodBody);
        var controlBlocks = cfa.ControlBlocks;
        var dotGraph = cfa.SerializeToGraphviz();
        Console.WriteLine(dotGraph);

        HashSet<uint> foreachLoopHeadOffsets = [];

        Dictionary<string, TypeSchema> scopedLocals = [];
        Stack<object> stack = new();

        for (int i = 0; i < methodBody.Length; i++)
        {
            var instruction = methodBody[i];

            var opCode = instruction.OpCode;
            var offset = instruction.Offset;

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
                            Source = IrisExpression.ToSyntax(stack.Pop(), offset, _context),
                        };

                        var foreachBlock = new CodeBlock(offset, loopBodyEndOffset, forEachBlockInfo);
                        cfa.PushBlock(foreachBlock);

                        break;

                    case OpCode.MethodInvokePeek:
                        // Ignore MoveNext calls when in a foreach loop, as long as we haven't already initialized this loop
                        if (!cfa.TryPeekBlockInfo<ForEachBlockInfo>(out var forEachBlockInfo1) || forEachBlockInfo1.Type is not null)
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
                        stack.Push(IrisExpression.ToSyntax(constant, offset, _context));
                        break;

                    case OpCode.PushNull:
                        stack.Push(LiteralExpression(SyntaxKind.NullLiteralExpression)
                            .WithOffset(offset));
                        break;

                    case OpCode.DiscardValue:
                        if (stack.Pop() is ExpressionSyntax expr)
                        {
                            if (expr is ParenthesizedExpressionSyntax parenExpr)
                                expr = parenExpr.Expression;

                            if (cfa.TryGetLastStatement(out var lastStatement))
                            {
                                if (lastStatement.DescendantNodes().Any(n => n.IsEquivalentTo(expr)))
                                    break;
                            }

                            cfa.AppendToBlock(ExpressionStatement(expr));
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
                        var newSymbolValueExpr = SimplifyExpression(IrisExpression.ToSyntax(newSymbolValue, offset, _context), true);

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

                        cfa.AppendToBlock(symbolWriteExpr.WithOffset(offset));
                        break;

                    case OpCode.PropertyAssign:
                    case OpCode.PropertyAssignStatic:
                        var propToSet = _context.GetImportedProperty(instruction.Operands.First());

                        var propSetTarget = opCode is OpCode.PropertyAssignStatic
                            ? propToSet.Owner
                            : stack.Pop();

                        var newPropValue = IrisExpression.ToSyntax(stack.Peek(), offset, _context);

                        var propertySetExpression = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IrisExpression.ToSyntax(propSetTarget, offset, _context),
                                IdentifierName(propToSet.Name)
                            ),
                            newPropValue
                        );

                        cfa.AppendToBlock(ExpressionStatement(propertySetExpression).WithOffset(offset));
                        break;

                    case OpCode.PropertyGetPeek:
                        // PGETP is only used in foreach loops

                        if (!cfa.TryPeekBlockInfo<ForEachBlockInfo>(out var forEachBlockInfo2))
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

                        scopedLocals[loopVariableSymbol] = loopVariableType;

                        stack.Push(IdentifierName(loopVariableSymbol).WithOffset(offset));

                        break;

                    case OpCode.VerifyTypeCast:
                        var objToCast = stack.Pop();
                        var typeToCastTo = _context.GetImportedType(instruction.Operands.First());

                        var castExpr = CastExpression(
                            IrisExpression.ToSyntax(typeToCastTo, _context),
                            IrisExpression.ToSyntax(objToCast, offset, _context)
                        );

                        stack.Push(Parenthesize(castExpr).WithOffset(offset));
                        break;

                    case OpCode.JumpIfFalse:
                    case OpCode.JumpIfFalsePeek:
                    case OpCode.JumpIfTruePeek:
                        var jumpToOffset = (uint)instruction.Operands.First().Value;

                        if (opCode is OpCode.JumpIfFalse && cfa.TryPeekBlockInfo<ForEachBlockInfo>(out var jmpfForEachBlockInfo)
                            && jmpfForEachBlockInfo.Type is null)
                            break;

                        var isPeek = opCode is OpCode.JumpIfFalsePeek or OpCode.JumpIfTruePeek;
                        var jumpCondition = IrisExpression.ToSyntax(isPeek ? stack.Peek() : stack.Pop(), offset, _context);

                        if (opCode is OpCode.JumpIfFalse)
                        {
                            // JMPF is used to evaluate the branch condition
                            var ifBlockEndOffset = methodBody
                               .Reverse()
                               .SkipWhile(i => i.Offset >= jumpToOffset)
                               .First()
                               .Offset;

                            var ifBlock = new CodeBlock(offset, ifBlockEndOffset, new IfBlockInfo(jumpCondition));
                            cfa.PushBlock(ifBlock);
                        }
                        else
                        {
                            // JMPFP and JMPTP are only used to implement short-circuiting
                            var ifCondition = SimplifyExpression(jumpCondition);
                            stack.Push(ifCondition);
                        }

                        break;

                    case OpCode.Jump:
                        var jumpOffset = (uint)instruction.Operands.First().Value;

                        CodeBlock currentForEachBlock = null;
                        ForEachBlockInfo jmpForEachBlockInfo = null;

                        foreach (var block in cfa.BlockStack)
                        {
                            currentForEachBlock = block;
                            jmpForEachBlockInfo = block.Info as ForEachBlockInfo;

                            if (jmpForEachBlockInfo is not null)
                                break;
                        }

                        if (jumpOffset < offset)
                        {
                            if (!foreachLoopHeadOffsets.Contains(jumpOffset))
                                throw new NotImplementedException("For and while loops are not supported at this time.");

                            if (currentForEachBlock is null)
                                throw new NotImplementedException($"Unexpected backwards JMP at 0x{offset:X}");

                            // Continue statements look like premature jumps back to the loop header
                            if (currentForEachBlock.EndOffset > offset)
                                cfa.AppendToBlock(ContinueStatement().WithOffset(offset));
                        }
                        else
                        {
                            // Break statements look like premature jumps to the loop tail
                            if (currentForEachBlock is not null && currentForEachBlock.EndOffset < jumpOffset)
                            {
                                cfa.AppendToBlock(BreakStatement().WithOffset(offset));
                            }
                            else
                            {
                                // End of if block, skipping over else block

                                // Figure out where the else block ends by searching for the last instruction we skip
                                var elseBlockEndOffset = methodBody
                                    .Reverse()
                                    .SkipWhile(i => i.Offset >= jumpOffset)
                                    .First()
                                    .Offset;

                                var elseBlock = new CodeBlock(offset, elseBlockEndOffset, new ElseBlockInfo());
                                cfa.PushBlock(elseBlock);
                            }
                        }

                        break;

                    case OpCode.ReturnValue:
                        var returnStatement = ReturnStatement(IrisExpression.ToSyntax(stack.Pop(), offset, _context));
                        cfa.AppendToBlock(returnStatement.WithOffset(offset));
                        break;

                    case OpCode.ReturnVoid:
                        // Include return statement when we're not in the main block (which would return anyway)
                        // or when we're not at the end of the function
                        if (cfa.BlockStack.Count > 1 || i + 1 < methodBody.Length)
                            cfa.AppendToBlock(ReturnStatement().WithOffset(offset));
                        break;

                    case OpCode.ClearSymbol:
                        // Ignore these instructions
                        break;

                    default:
                        if (!TryDecompileExpression(instruction, stack, cfa))
                        {
                            var unsupportedComment = Comment($"// Unsupported instruction: {instruction}");
                            cfa.AppendToBlock(EmptyStatement()
                                .WithLeadingTrivia(unsupportedComment)
                                .WithOffset(offset));
                        }
                        break;
                }

                cfa.FinalizeCompletedBlocks(offset);
            }
            catch (Exception ex)
            {
                Console.WriteLine("oopsie");
                return null;
                throw new Exception($"Failed to decompile instruction `{instruction}` @ 0x{instruction.Offset:X} in script for {export.Name}", ex);
            }
        }

        if (cfa.BlockStack.Count > 1)
            throw new InvalidOperationException($"Failed to decompile script for {export.Name}, more than one top-level code block");
        else if (cfa.BlockStack.Count < 0)
            throw new InvalidOperationException($"Failed to decompile script for {export.Name}, no top-level code blocks");

        // Unwrap top-most block to avoid extra curly braces around entire script
        return cfa.BlockStack.Pop().Statements;
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
            var offset = instruction.Offset;

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
                            IrisExpression.ToSyntax(propGetTarget, offset, _context),
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

                            // TODO: remove me
                            if (statements is null)
                                break;

                            scriptContent = CreateTree(statements, scriptOffset);
                            _context.SetScriptContent(initType, scriptOffset, scriptContent);
                        }

                        var scriptRoot = scriptContent.GetRoot();

                        if (listenerType is not ListenerType.Symbol)
                        {
                            var memberAccessExpr = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IrisExpression.ToSyntax(handlerObj, offset, _context),
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

    public static SyntaxTree CreateTree(IEnumerable<StatementSyntax> statements, uint? startOffset = null)
    {
        var root = CompilationUnit()
            .WithMembers(
                [.. statements.Select(GlobalStatement)]
            );

        if (startOffset is not null)
            root = root.WithOffset(startOffset.Value);

        return SyntaxTree(root);
    }

    public string FormatInlineExpression(ExpressionSyntax expr, CancellationToken token = default) => '{' + FormatSyntaxNode(expr, token) + '}';

    public string FormatScript(SyntaxTree tree, CancellationToken token = default) => FormatSyntaxNode(tree.GetRoot(token), token);

    public string FormatSyntaxNode(SyntaxNode root, CancellationToken cancellationToken = default)
    {
        uint? scriptOffset = null;
        ScriptDebugSymbols debugSymbols = new()
        {
            SourceMap = []
        };

        root = root.NormalizeWhitespace();

        foreach (var node in root.GetAnnotatedNodes(UIBOffsetAnnotation.Kind))
        {
            var offsetAnnotation = node.GetAnnotations(UIBOffsetAnnotation.Kind).Single();
            var offset = UIBOffsetAnnotation.GetOffset(offsetAnnotation);

            var location = node.GetLocation();

            debugSymbols.SourceMap[offset] = new(location.SourceSpan.Start, location.SourceSpan.End);

            if (scriptOffset is null && node is CompilationUnitSyntax)
                scriptOffset = offset;
        }

        var sourceText = root
            .SyntaxTree
            .GetText(cancellationToken);

        debugSymbols.SourceCode = sourceText.ToString();

        if (scriptOffset is not null)
            _context.DebugSymbols.ScriptSymbols[scriptOffset.Value] = debugSymbols;

        return debugSymbols.SourceCode;
    }

    private bool TryDecompileExpression(Instruction instruction, Stack<object> stack, ControlFlowAnalyzer cfa = null)
    {
        var opCode = instruction.OpCode;
        var offset = instruction.Offset;

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
                        var parameter = IrisExpression.ToSyntax(stack.Pop(), offset, _context);
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
                    var parameter = IrisExpression.ToSyntax(stack.Pop(), offset, _context);
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

                var methodTargetExpression = IrisExpression.ToSyntax(targetObj, offset, _context);

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
                    cfa?.AppendToBlock(ExpressionStatement(methodResult));
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
                    IrisExpression.ToSyntax(propGetTarget, offset, _context),
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
                    IrisExpression.ToSyntax(objToCheck, offset, _context),
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
                    Parenthesize(IrisExpression.ToSyntax(stack.Pop(), offset, _context))
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
        var offset = instruction.Offset;
        var op = instruction.OperationType.Value;
        var isUnary = TypeSchema.IsUnaryOperation(op);
        var opSyntax = OperationToSyntaxKind(op);

        ExpressionSyntax operationExpr;

        if (isUnary)
        {
            var left = Parenthesize(IrisExpression.ToSyntax(stack.Pop(), offset, _context));
            var isPostfix = op is OperationType.PostIncrement or OperationType.PostDecrement;

            operationExpr = isPostfix
                ? PostfixUnaryExpression(opSyntax, left)
                : PrefixUnaryExpression(opSyntax, left);
        }
        else
        {
            var right = IrisExpression.ToSyntax(stack.Pop(), offset, _context);
            var left = IrisExpression.ToSyntax(stack.Pop(), offset, _context);

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

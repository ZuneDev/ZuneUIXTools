using Sprache;
using Microsoft.Iris.Asm;
using Xunit.Abstractions;
using Microsoft.Iris.Markup;
using UIX.Test.Fixtures;
using Microsoft.Iris;

namespace UIX.Test;

[Collection(MarkupSystemCollection.Name)]
public class Assembly(ITestOutputHelper output)
{
    [Fact]
    public void Parse()
    {
        const string code =
"""
.import-ns Me as me
.import-ns assembly://UIX/Microsoft.Iris as iris

main:
    COBJ 2
    PSHC 0
    PINI 1
    PSHC 1
    PINI 2
    PINI 0
    RETV
    RETV
""";

        var ast = Lexer.Program.Parse(code);
        output.WriteLine(ast.ToString());

        Assert.NotNull(ast);
        Assert.Equal(2, ast.Imports.Count());
        Assert.Equal(9, ast.Body.Count());
    }

    [Fact]
    public async Task Assemble()
    {
        using TempFile tempFile = new("testA.uixa", ".uixa");
        await tempFile.InitAsync();

        CompilerInput[] compilerInputs = [
            new()
            {
                SourceFileName = tempFile.Path,
                OutputFileName = Path.ChangeExtension(tempFile.Path, ".uib")
            }
        ];

        foreach (var compilerInput in compilerInputs)
            output.WriteLine(compilerInput.OutputFileName);

        MarkupCompiler.Compile(compilerInputs, default);
    }

    [Theory]
    [InlineData("text.uib")]
    [InlineData("text.uix")]
    [InlineData("testA.uix")]
    [InlineData("testB.uix")]
    public async Task Disassemble(string testResourceName)
    {
        using TempFile tempFile = new(testResourceName);
        await tempFile.InitAsync();

        string uri = "file://" + tempFile.Path;

        var sourceLoadResult = MarkupSystem.ResolveLoadResult(uri, MarkupSystem.RootIslandId);
        var dis = Disassembler.Load(sourceLoadResult as MarkupLoadResult);

        var disText = dis.Write();
        output.WriteLine(disText);
    }
}
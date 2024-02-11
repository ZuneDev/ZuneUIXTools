using Sprache;
using Microsoft.Iris.Asm;
using Xunit.Abstractions;
using Microsoft.Iris.Markup;
using UIX.Test.Fixtures;
using Microsoft.Iris;
using Microsoft.Iris.Session;

namespace UIX.Test;

[Collection(MarkupSystemCollection.Name)]
public class Assembly(ITestOutputHelper output)
{
    [Fact]
    public void Parse()
    {
        const string code =
"""
.export main 0 UI

.import-ns Me as me
.import-ns assembly://UIX/Microsoft.Iris as iris
.import-type iris:Command
.import-type Int32


.section data

.section object
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
        Assert.Equal(3 + 2 + 2, ast.Directives.Count());
        Assert.Equal(9, ast.Body.Count());
    }

    [Theory]
    [InlineData("text")]
    public async Task ReassembleFromUIB(string fileNameWithoutExtension)
    {
        using TempFile uibFile = new($"{fileNameWithoutExtension}.uib", ".uib");
        await uibFile.InitAsync();

        var uixaPath = Path.ChangeExtension(uibFile.Path, ".uixa");
        var uibPathAc = Path.ChangeExtension(uibFile.Path, ".g.uib");

        // Disassemble the compiled UIX
        var sourceLoadResult = MarkupSystem.ResolveLoadResult($"file://{uibFile.Path}", MarkupSystem.RootIslandId);
        var disassembly = Disassembler.Load(sourceLoadResult as MarkupLoadResult);
        var disassemblyText = disassembly.Write();
        File.WriteAllText(uixaPath, disassemblyText);

        // Compile the generated Assembly source
        CompilerInput compilerInput = new()
        {
            SourceFileName = uixaPath,
            OutputFileName = uibPathAc
        };

        output.WriteLine($"Expected: {uibFile.Path}");
        output.WriteLine($"Actual: {compilerInput.OutputFileName}");

        ErrorManager.OnErrors += (errors) =>
        {
            foreach (ErrorRecord error in errors)
                output.WriteLine($"Error at (L{error.Line}, C{error.Column}): {error.Message}");
        };

        // Assemble the disassembly result
        var success = MarkupCompiler.Compile([compilerInput], default);
        Assert.True(success);

        // Compare the original UIB file to the reassembled UIB
        var uibBytesEx = await File.ReadAllBytesAsync(uibFile.Path);
        var uibBytesAc = await File.ReadAllBytesAsync(compilerInput.OutputFileName);
        Assert.Equal(uibBytesEx, uibBytesAc);
    }

    [Theory]
    [InlineData("testA")]
    public async Task Assemble(string fileNameWithoutExtension)
    {
        using TempFile asmFile = new($"{fileNameWithoutExtension}.uixa", ".uixa");
        await asmFile.InitAsync();

        CompilerInput[] compilerInputs = [
            new()
            {
                SourceFileName = asmFile.Path,
                OutputFileName = Path.ChangeExtension(asmFile.Path, ".uib")
            }
        ];

        foreach (var compilerInput in compilerInputs)
            output.WriteLine(compilerInput.OutputFileName);

        ErrorManager.OnErrors += (errors) =>
        {
            foreach (ErrorRecord error in errors)
                output.WriteLine($"Error at (L{error.Line}, C{error.Column}): {error.Message}");
        };

        var success = MarkupCompiler.Compile(compilerInputs, default);
        Assert.True(success);
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
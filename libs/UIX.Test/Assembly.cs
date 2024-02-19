using Sprache;
using Microsoft.Iris.Asm;
using Xunit.Abstractions;
using Microsoft.Iris.Markup;
using UIX.Test.Fixtures;
using Microsoft.Iris;
using Microsoft.Iris.Session;
using Microsoft.Iris.Debug;

namespace UIX.Test;

[Collection(MarkupSystemCollection.Name)]
public class Assembly(ITestOutputHelper output)
{
    [Fact]
    public void Parse()
    {
        const string code =
"""
.export Default 0 UI
.export Alt 0 UI

.import-type UI
.import-type Dictionary
.import-ns assembly://UIX/Microsoft.Iris as iris
.import-type iris:Command
.import-type String
.import-type ViewItem
.import-type Text
.import-type Color
.import-type Font
.import-mbrs UI{Locals, Content}
.import-mbrs Text{Color, Content, Font}

.constant const0 = Color(255, 255, 0, 0)
.constant const1 = String(Howdy from Microsoft.Iris!)
.constant const2 = String(SelectCommand)
.constant const3 = Color(255, 0, 0, 255)
.constant const4 = Font(JetBrains Mono)
.constant const5 = String(This is some blue text)
.section object

Default_cont:
    COBJ 5
    PSHC @const0
    PINI 2
    PSHC @const1
    PINI 3
    PINI 1
    RETV
Default_locl:
    COBJ 2
    PDAD 0, @const2
    RETV
Alt_cont:
    COBJ 5
    PSHC @const3
    PINI 2
    PSHC @const4
    PINI 4
    PSHC @const5
    PINI 3
    PINI 1
    RETV
Alt_locl:
    RETV
""";

        var ast = Lexer.Program.Parse(code);
        output.WriteLine(ast.ToString());

        Assert.NotNull(ast);
        Assert.Equal(20, ast.Directives.Count());
        Assert.Equal(24, ast.Code.Count());
    }

    [Theory]
    [InlineData("text")]
    public async Task ReassembleFromUIB(string fileNameWithoutExtension)
    {
        using TempFile uibFile = new($"{fileNameWithoutExtension}.uib", ".uib");
        await uibFile.InitAsync();

        var uixaPath = Path.ChangeExtension(uibFile.Path, ".uixa");
        var uibPathAc = Path.ChangeExtension(uibFile.Path, ".g.uib");

        // Configure debugging stuff
        ErrorManager.OnErrors += (errors) =>
        {
            foreach (ErrorRecord error in errors)
                output.WriteLine($"Error at (L{error.Line}, C{error.Column}): {error.Message}");
        };
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);
        TraceSettings.Current.OnWriteLine += output.WriteLine;

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

        // Assemble the disassembly result
        var success = MarkupCompiler.Compile([compilerInput], default);
        Assert.True(success);

        // Compare the original UIB file to the reassembled UIB
        var uibBytesEx = await File.ReadAllBytesAsync(uibFile.Path);
        var uibBytesAc = await File.ReadAllBytesAsync(compilerInput.OutputFileName);
        //Assert.Equal(uibBytesEx, uibBytesAc);


        var actualLoadResult = MarkupSystem.ResolveLoadResult($"file://{compilerInput.OutputFileName}", (uint)Random.Shared.Next());
        actualLoadResult.FullLoad();
        AssertEx.DeepEquivalent(sourceLoadResult, actualLoadResult);
    }

    [Theory]
    [InlineData("testA")]
    public async Task ReassembleFromUIX(string fileNameWithoutExtension)
    {
        using TempFile uixFile = new($"{fileNameWithoutExtension}.uix", ".uix");
        await uixFile.InitAsync();

        var uixaPath = Path.ChangeExtension(uixFile.Path, ".uixa");
        var uibPathEx = Path.ChangeExtension(uixFile.Path, ".uib");
        var uibPathAc = Path.ChangeExtension(uixFile.Path, ".g.uib");

        // Configure debugging stuff
        ErrorManager.OnErrors += (errors) =>
        {
            foreach (ErrorRecord error in errors)
                output.WriteLine($"Error at (L{error.Line}, C{error.Column}): {error.Message}");
        };
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);
        TraceSettings.Current.OnWriteLine += output.WriteLine;

        CompilerInput uixCompilerInput = new()
        {
            SourceFileName = uixFile.Path,
            OutputFileName = uibPathEx
        };
        CompilerInput asmCompilerInput = new()
        {
            SourceFileName = uixaPath,
            OutputFileName = uibPathAc
        };

        output.WriteLine($"Source: {uixFile.Path}");
        output.WriteLine($"Expected: {uibPathEx}");
        output.WriteLine($"Actual: {uibPathAc}");

        ErrorManager.OnErrors += (errors) =>
        {
            foreach (ErrorRecord error in errors)
                output.WriteLine($"Error at (L{error.Line}, C{error.Column}): {error.Message}");
        };

        // Compile the UIX source
        var uixSuccess = MarkupCompiler.Compile([uixCompilerInput], default);
        Assert.True(uixSuccess);

        // Disassemble the compiled UIX
        var sourceLoadResult = MarkupSystem.ResolveLoadResult($"file://{uixFile.Path}", MarkupSystem.RootIslandId);
        var disassembly = Disassembler.Load(sourceLoadResult as MarkupLoadResult);
        var disassemblyText = disassembly.Write();
        File.WriteAllText(uixaPath, disassemblyText);

        // Compile the generated Assembly source
        var asmSuccess = MarkupCompiler.Compile([asmCompilerInput], default);
        Assert.True(asmSuccess);

        // Load assembled UIB and compare with original result
        var actualLoadResult = MarkupSystem.ResolveLoadResult($"file://{uibPathAc}", (uint)Random.Shared.Next());
        actualLoadResult.FullLoad();

        AssertEx.DeepEquivalent(sourceLoadResult, actualLoadResult);

        if (false)
        {
            // Compare the original UIB file to the reassembled UIB
            var uibBytesEx = await File.ReadAllBytesAsync(uibPathEx);
            var uibBytesAc = await File.ReadAllBytesAsync(asmCompilerInput.OutputFileName);
            Assert.Equal(uibBytesEx, uibBytesAc);
        }
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
            {
                var errorTypeText = error.Warning ? "Warning" : "Error";
                output.WriteLine($"{errorTypeText} at (L{error.Line}, C{error.Column}): {error.Message}");
            }
        };

        var success = MarkupCompiler.Compile(compilerInputs, default);
        Assert.True(success);

        var compiledLoadResult = MarkupSystem.ResolveLoadResult($"file://{compilerInputs[0].OutputFileName}", MarkupSystem.RootIslandId);
        compiledLoadResult.FullLoad();
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
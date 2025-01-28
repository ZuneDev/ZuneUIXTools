using Sprache;
using Microsoft.Iris.Asm;
using Xunit.Abstractions;
using Microsoft.Iris.Markup;
using UIX.Test.Fixtures;
using Microsoft.Iris;
using Microsoft.Iris.Debug;

namespace UIX.Test;

[Collection(MarkupSystemCollection.Name)]
public class Assembly
{
    private readonly ITestOutputHelper _output;

    public Assembly(ITestOutputHelper output, MarkupSystemFixture markupSystemFixture)
    {
        _output = output;
        markupSystemFixture.SetupDebug(output);
    }

    [Fact]
    public void Parse()
    {
        const string code =
"""
.export Default 0 UI
.export Alt 0 UI

.import-ns assembly://UIX/Microsoft.Iris as iris
.import-type UI
.import-type Dictionary
.import-type iris:Command
.import-type String
.import-type ViewItem
.import-type Text
.import-type Color
.import-type Font
.import-type generic:List`1[System.String]
.import-mbrs UI{Locals, Content}
.import-mbrs Text{Color, Content, Font}
.import-mbrs SingletonModelItem`1[ZuneUI.TransportControls]{Instance}
.import-mthd List`1[System.Int32].Add(Int32)

.constant const0 = Color.str(255, 255, 0, 0)
.constant const1 = String.str(Howdy from Microsoft.Iris!)
.constant const2 = String.str(SelectCommand)
.constant const3 = Color.str(255, 0, 0, 255)
.constant const4 = Font.str(JetBrains Mono)
.constant const5 = String.str(This is some blue text)
.constant const3255 = String.str(({0:X8}))
.constant const3256 = String("Content\nOn a new line")
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
        _output.WriteLine(ast.ToString());

        Assert.NotNull(ast);
        Assert.Equal(25, ast.Directives.Count());
        Assert.Equal(24, ast.Code.Count());
    }

    [Fact]
    public void ParseWithDataTable()
    {
        const string code =
"""
.export Default 0 UI
.export Alt 0 UI

.dataTable res://ZuneShellResources.dll!_DataTable.uib

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
        _output.WriteLine(ast.ToString());

        Assert.NotNull(ast);
        Assert.Equal(4, ast.Directives.Count());
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
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);

        // Disassemble the compiled UIX
        var sourceLoadResult = MarkupSystem.ResolveLoadResult($"file://{uibFile.Path}", MarkupSystem.RootIslandId);
        var disassembly = Disassembler.Load(sourceLoadResult);
        var disassemblyText = disassembly.Write();
        File.WriteAllText(uixaPath, disassemblyText);

        // Compile the generated Assembly source
        CompilerInput compilerInput = new()
        {
            SourceFileName = uixaPath,
            OutputFileName = uibPathAc
        };

        _output.WriteLine($"Disassembly: {uixaPath}");
        _output.WriteLine($"Expected: {uibFile.Path}");
        _output.WriteLine($"Actual: {compilerInput.OutputFileName}");

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
    [InlineData("text")]
    [InlineData("testA")]
    public async Task ReassembleFromUIX(string fileNameWithoutExtension)
    {
        using TempFile uixFile = new($"{fileNameWithoutExtension}.uix", ".uix");
        await uixFile.InitAsync();

        var uixaPath = Path.ChangeExtension(uixFile.Path, ".uixa");
        var uibPathEx = Path.ChangeExtension(uixFile.Path, ".uib");
        var uibPathAc = Path.ChangeExtension(uixFile.Path, ".g.uib");

        // Configure debugging stuff
        TraceSettings.Current.SetCategoryLevel(TraceCategory.Markup, byte.MaxValue);
        TraceSettings.Current.SetCategoryLevel(TraceCategory.MarkupCompiler, byte.MaxValue);

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

        _output.WriteLine($"Source: {uixFile.Path}");
        _output.WriteLine($"Expected: {uibPathEx}");
        _output.WriteLine($"Actual: {uibPathAc}");

        // Compile the UIX source
        var uixSuccess = MarkupCompiler.Compile([uixCompilerInput], default);
        Assert.True(uixSuccess);

        // Disassemble the compiled UIX
        var sourceLoadResult = MarkupSystem.ResolveLoadResult($"file://{uixFile.Path}", MarkupSystem.RootIslandId);
        var disassembly = Disassembler.Load(sourceLoadResult);
        var disassemblyText = disassembly.Write();
        File.WriteAllText(uixaPath, disassemblyText);

        // Compile the generated Assembly source
        var asmSuccess = MarkupCompiler.Compile([asmCompilerInput], default);
        Assert.True(asmSuccess);

        // Load assembled UIB and compare with original result
        var actualLoadResult = MarkupSystem.ResolveLoadResult($"file://{uibPathAc}", (uint)Random.Shared.Next());
        actualLoadResult.FullLoad();

        AssertEx.DeepEquivalent(sourceLoadResult, actualLoadResult);
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
            _output.WriteLine(compilerInput.OutputFileName);

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
        var dis = Disassembler.Load(sourceLoadResult);

        var disText = dis.Write();
        _output.WriteLine(disText);
    }
}
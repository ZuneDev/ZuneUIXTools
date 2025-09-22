using Microsoft.Iris;
using Microsoft.Iris.DecompXml;
using Microsoft.Iris.Markup;
using UIX.Test.Fixtures;
using Xunit.Abstractions;

namespace UIX.Test;

[Collection(MarkupSystemCollection.Name)]
public class DecompileControlFlow
{
    private readonly ITestOutputHelper _output;

    public DecompileControlFlow(ITestOutputHelper output, MarkupSystemFixture markupSystemFixture)
    {
        _output = output;
        markupSystemFixture.SetupDebug(output);
    }

    [Theory]
    [InlineData("cfa_if-elseif-else01")]
    [InlineData("cfa_if-elseif-else02")]
    public async Task IfElseIfElse(string fileNameWithoutExtension)
    {
        using TempFile uixFile = new($"{fileNameWithoutExtension}.uix", ".uix");
        await uixFile.InitAsync();

        var uibPathEx = Path.ChangeExtension(uixFile.Path, ".uib");

        // Compile the UIX source
        CompilerInput uixCompilerInput = new()
        {
            SourceFileName = uixFile.Path,
            OutputFileName = uibPathEx
        };
        var uixSuccess = MarkupCompiler.Compile([uixCompilerInput], default);
        Assert.True(uixSuccess);

        // Decompile the compiled UIX
        var sourceLoadResult = MarkupSystem.ResolveLoadResult($"file://{uixFile.Path}", MarkupSystem.RootIslandId);
        var decompiler = Decompiler.Load(sourceLoadResult);
        var uixDocAc = decompiler.Decompile();

        var uixScriptAc = uixDocAc.Descendants()
            .Where(e => e.Name.LocalName == "Script")
            .FirstOrDefault()?.Value;
        Assert.NotNull(uixScriptAc);
        _output.WriteLine(uixScriptAc);
    }
}

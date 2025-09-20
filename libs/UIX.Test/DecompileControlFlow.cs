using Microsoft.Iris;
using Microsoft.Iris.DecompXml;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

    [Fact]
    public async Task IfElseIfElse()
    {
        using TempFile uixFile = new($"cfa_if-elseif-else.uix", ".uix");
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

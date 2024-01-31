using Sprache;
using Microsoft.Iris.Asm;
using Xunit.Abstractions;
using Microsoft.Iris.Markup;

namespace UIX.Test;

public class Assembly(ITestOutputHelper output)
{
    [Fact]
    public void Test1()
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
    public async Task Disassemble()
    {
        const string path = @"D:\Repos\yoshiask\ZuneUIXTools\test\testA.uix";
        string uri = @"file://" + path;

        //FileResource resource = new(uri, path, true);
        //resource.Acquire();

        MarkupSystem.Startup(true);
        var sourceLoadResult = MarkupSystem.ResolveLoadResult(uri, MarkupSystem.RootIslandId);
        var dis = Disassembler.Load(sourceLoadResult as MarkupLoadResult);

        var newPath = Path.ChangeExtension(path, ".uixa");
        File.WriteAllText(newPath, dis.Write());

        MarkupSystem.Shutdown();
    }
}
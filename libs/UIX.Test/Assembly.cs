using Sprache;
using UIX.Asm;

namespace UIX.Test;

public class Assembly
{
    [Fact]
    public void Test1()
    {
        const string code =
"""
.import Me as me
.import assembly://UIX/Microsoft.Iris as iris

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
        Assert.NotNull(ast);
        Assert.Equal(2, ast.Imports.Count());
        Assert.Equal(9, ast.Body.Count());
    }
}
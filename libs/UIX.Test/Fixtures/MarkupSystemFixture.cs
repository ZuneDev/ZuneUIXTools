using Microsoft.Iris.Asm;
using Microsoft.Iris.Markup;

namespace UIX.Test.Fixtures;

public class MarkupSystemFixture : IDisposable
{
    public MarkupSystemFixture()
    {
        MarkupSystem.Startup(true);
        Assembler.RegisterLoader();
    }

    public void Dispose() => MarkupSystem.Shutdown();
}

[CollectionDefinition("MarkupSystem")]
public class MarkupSystemCollection : ICollectionFixture<MarkupSystemFixture>
{
    public const string Name = "MarkupSystem";

    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

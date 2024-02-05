namespace Microsoft.Iris.Asm.Models;

public interface IAsmItem
{
    public int Line { get; set; }
    public int Column { get; set; }
}

public abstract record AsmItem : IAsmItem
{
    public int Line { get; set; } = -1;
    public int Column { get; set; } = -1;

    internal const string DebuggerDisplay = "({Line}, {Column})";
}

public interface IDirective : IAsmItem
{
    string Identifier { get; init; }
}
public abstract record Directive(string Identifier) : AsmItem, IDirective
{
    public override string ToString() => $".{Identifier}";
}

public interface IImport : IDirective;
public abstract record Import : Directive, IImport
{
    public Import(string Type) : base($"import-{Type}")
    {
    }

    public override string ToString() => base.ToString();
}

public interface IBodyItem : IAsmItem;
public abstract record BodyItem : AsmItem, IBodyItem;

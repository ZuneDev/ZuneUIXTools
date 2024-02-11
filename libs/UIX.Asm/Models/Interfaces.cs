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

public interface IBodyItem : IAsmItem;
public abstract record BodyItem : AsmItem, IBodyItem;

public interface IDirective : IBodyItem
{
    string Identifier { get; init; }
}
public abstract record Directive(string Identifier) : AsmItem, IDirective
{
    public override string ToString() => $".{Identifier}";
}

public interface IImportDirective : IDirective;
public abstract record ImportDirective : Directive, IImportDirective
{
    public ImportDirective(string Type) : base($"import-{Type}")
    {
    }

    public override string ToString() => base.ToString();
}

public interface ICodeItem : IBodyItem;
public abstract record CodeItem : BodyItem, ICodeItem;

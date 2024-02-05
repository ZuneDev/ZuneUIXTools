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
}

public interface IDirective : IAsmItem;
public abstract record Directive : AsmItem, IDirective;

public interface IImport : IDirective;
public abstract record Import : Directive;

public interface IBodyItem : IAsmItem;
public abstract record BodyItem : AsmItem, IBodyItem;

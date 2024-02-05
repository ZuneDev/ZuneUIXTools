namespace Microsoft.Iris.Asm.Models;

public record SectionDirective : Directive, IBodyItem
{
    public SectionDirective(string name) : base("section")
    {
        Name = name;
    }

    public string Name { get; init; }

    public override string ToString() => $"{base.ToString()} {Name}";
}

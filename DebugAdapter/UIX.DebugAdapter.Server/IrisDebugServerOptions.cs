namespace Microsoft.Iris.DebugAdapter;

public class IrisDebugServerOptions
{
    public required string ConnectionString { get; set; }

    public required string SymbolDir { get; set; }

    public string? SourceDir { get; set; }
}

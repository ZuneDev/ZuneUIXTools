using Errata;
using Microsoft.Iris;
using System.Diagnostics.CodeAnalysis;
namespace UIXC;

internal class IrisSourceRepository : ISourceRepository
{
    private readonly Dictionary<string, Source?> _sourceCache;

    public IrisSourceRepository(CompilerInput[] compilands, CompilerInput? sharedBinaryDataTable)
    {
        _sourceCache = new(compilands.Length + 1);

        foreach (var compiland in compilands)
        {
            var id = compiland.SourceFileName;
            _sourceCache.Add(id, null);
        }

        if (sharedBinaryDataTable?.SourceFileName is not null)
            _sourceCache.Add(sharedBinaryDataTable.Value.SourceFileName, null);
    }

    public bool TryGet(string id, [NotNullWhen(true)] out Source? source)
    {
        if (!_sourceCache.TryGetValue(id, out source))
            throw new ArgumentException($"No compiland with ID '{id}' was loaded.");

        if (source is null)
        {
            var sourceText = File.ReadAllText(id);
            source = _sourceCache[id] = new(id, sourceText);
        }

        return true;
    }
}

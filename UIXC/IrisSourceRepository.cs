using Errata;
using Microsoft.Iris;
using System.Diagnostics.CodeAnalysis;

namespace UIXC;

public class IrisSourceRepository(IEnumerable<string?> compilands) : ISourceRepository
{
    private readonly Dictionary<string, Source?> _sourceCache = compilands
        .Where(c => c is not null).Cast<string>()
        .Where(c => !c.Contains(Uri.SchemeDelimiter))
        .Select(c => (c, (Source?)null))
        .ToDictionary();

    public IrisSourceRepository(CompilerInput[] compilands, CompilerInput? sharedBinaryDataTable)
        : this(compilands.Select(c => c.SourceFileName).AsEnumerable().Append(sharedBinaryDataTable.HasValue ? sharedBinaryDataTable.Value.SourceFileName : null))
    {
    }

    public bool TryGet(string uri, [NotNullWhen(true)] out Source? source)
    {
        var idStart = uri.IndexOf(Uri.SchemeDelimiter) + Uri.SchemeDelimiter.Length;
        var id = uri[idStart..];
        
        if (!_sourceCache.TryGetValue(id, out source))
            return false;

        if (source is null)
        {
            var sourceText = File.ReadAllText(id);
            source = _sourceCache[id] = new(id, sourceText);
        }

        return true;
    }
}

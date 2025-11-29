using Microsoft.Iris.Debug.Symbols;

namespace UIXC;

public class DebugSymbolResolver(string symbolDir)
{
    private readonly DirectoryInfo _symbolDir = new(symbolDir);

    public ApplicationDebugSymbols? GetForApplication(string application)
    {
        var fileName = $"{application}.asym.json";
        var asymFile = _symbolDir.EnumerateFiles()
            .FirstOrDefault(f => f.Name == fileName);

        if (asymFile is null)
            return null;

        using var stream = asymFile.OpenRead();
        using var reader = new StreamReader(stream);
        return DebugSymbolsJsonParser.ParseForApplication(reader.ReadToEnd());
    }

    public FileDebugSymbols? GetForFile(string file, string? application = null)
    {
        var fileName = $"{file}.fsym.json";
        var fsymFile = FindFile(fileName);

        if (fsymFile is not null)
        {
            using var stream = fsymFile.OpenRead();
            using var reader = new StreamReader(stream);
            return DebugSymbolsJsonParser.ParseForFile(reader.ReadToEnd());
        }
        
        if (application is null)
            return null;

        var asym = GetForApplication(application);
        if (asym is null)
            return null;

        return asym.Files.FirstOrDefault(f => f.CompiledFileName == file || f.SourceFileName == file);
    }

    private FileInfo? FindFile(string fileName)
    {
        return _symbolDir
            .EnumerateFiles()
            .FirstOrDefault(f => AreEquivalentFileNames(f.Name, fileName));
    }

    private static bool AreEquivalentFileNames(string fileName1, string fileName2)
    {
        // NOTE: Assumes Windows. Should be trivial to support other filesystems later.
        return fileName1.Equals(fileName2, StringComparison.InvariantCultureIgnoreCase);
    }
}

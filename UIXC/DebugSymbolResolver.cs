using Microsoft.Iris.Debug.Symbols;

namespace UIXC;

public class DebugSymbolResolver(string symbolDir, string? sourceDir)
{
    private readonly DirectoryInfo _symbolDir = new(symbolDir);
    private readonly DirectoryInfo? _sourceDir = sourceDir is not null ? new(sourceDir) : null;

    public ApplicationDebugSymbols? GetForApplication(string application)
    {
        var fileName = $"{application}.asym.json";
        var asymFile = FindFile(fileName, _symbolDir);

        if (asymFile is null)
            return null;

        using var stream = asymFile.OpenRead();
        using var reader = new StreamReader(stream);
        return DebugSymbolsJsonParser.ParseForApplication(reader.ReadToEnd());
    }

    public FileDebugSymbols? GetForFile(string file, string? application = null)
    {
        var fileName = $"{file}.fsym.json";
        var fsymFile = FindFile(fileName, _symbolDir);

        if (fsymFile is not null)
        {
            using var stream = fsymFile.OpenRead();
            using var reader = new StreamReader(stream);
            var fsym = DebugSymbolsJsonParser.ParseForFile(reader.ReadToEnd());

            var sourceFile = FindFile(file, _sourceDir);
            if (sourceFile is not null)
            {
                using var sourceStream = sourceFile.OpenRead();
                using var sourceReader = new StreamReader(sourceStream);
                fsym.SetSourceCode(sourceReader.ReadToEnd());
            }

            return fsym;
        }
        
        if (application is null)
            return null;

        var asym = GetForApplication(application);
        if (asym is null)
            return null;

        return asym.Files.FirstOrDefault(f => f.CompiledFileName == file || f.SourceFileName == file);
    }

    private static FileInfo? FindFile(string fileName, DirectoryInfo? dir)
    {
        return dir?
            .EnumerateFiles()
            .FirstOrDefault(f => AreEquivalentFileNames(f.Name, fileName));
    }

    private static bool AreEquivalentFileNames(string fileName1, string fileName2)
    {
        // NOTE: Assumes Windows. Should be trivial to support other filesystems later.
        return fileName1.Equals(fileName2, StringComparison.InvariantCultureIgnoreCase);
    }
}

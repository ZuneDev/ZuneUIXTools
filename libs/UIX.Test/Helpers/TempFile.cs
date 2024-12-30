using OwlCore.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PathIO = System.IO.Path;

namespace UIX.Test.Helpers;

internal class TempFile : IDisposable, IAsyncInit
{
    private const string _defaultFileExtension = ".tmp";
    private readonly Func<Task> _init;

    public TempFile(System.Resources.ResourceManager resourceManager, string resourceName, string fileExtension = _defaultFileExtension)
        : this((byte[])resourceManager.GetObject(resourceName)!, fileExtension)
    { 
    }

    public TempFile(string resourceName, string fileExtension = _defaultFileExtension) : this(TestResources.ResourceManager, resourceName, fileExtension)
    {
    }

    public TempFile(byte[] data, string fileExtension = _defaultFileExtension)
    {
        _init = async delegate
        {
            var fileName = $"uixtest_{PathIO.GetRandomFileName()}";
            fileName = PathIO.ChangeExtension(fileName.Replace(".", ""), fileExtension);
            Path = PathIO.Combine(PathIO.GetTempPath(), fileName);

            // Copy the test data to a temporary file because Iris can only load from a URI.
            await File.WriteAllBytesAsync(Path, data);
        };
    }

    public string? Path { get; private set; }

    [MemberNotNullWhen(true, nameof(Path))]
    public bool IsInitialized { get; private set; }

    public void Dispose()
    {
        if (!IsInitialized) return;

        File.Delete(Path);
    }

    [MemberNotNull(nameof(Path))]
    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
        await _init();
#pragma warning restore CS8774 // Member must have a non-null value when exiting.
    }
}

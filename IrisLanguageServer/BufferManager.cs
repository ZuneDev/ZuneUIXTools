using Microsoft.Language.Xml;
using System.Collections.Concurrent;

namespace IrisLanguageServer;

internal class BufferManager
{
    private readonly ConcurrentDictionary<string, Buffer> _buffers = new();

    public void UpdateBuffer(string documentPath, Buffer buffer)
    {
        _buffers.AddOrUpdate(documentPath, buffer, (k, v) => buffer);
    }

    public Buffer? GetBuffer(string documentPath)
    {
        return _buffers.TryGetValue(documentPath, out var buffer)
            ? buffer
            : null;
    }
}

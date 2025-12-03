using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Iris.DebugAdapter;

public class LoggingStream(Stream stream) : Stream
{
    public LoggingStream(Stream stream, EventHandler<ArraySegment<byte>>? onRead, EventHandler<ArraySegment<byte>>? onWrite)
        : this(stream)
    {
        OnRead += onRead;
        OnWrite += onWrite;
    }

    public Stream InnerStream { get; } = stream;

    public event EventHandler<ArraySegment<byte>>? OnRead;
    public event EventHandler<ArraySegment<byte>>? OnWrite;

    public override bool CanRead => InnerStream.CanRead;

    public override bool CanSeek => InnerStream.CanSeek;

    public override bool CanWrite => InnerStream.CanWrite;

    public override long Length => InnerStream.Length;

    public override long Position { get => InnerStream.Position; set => InnerStream.Position = value; }

    public override void Flush()
    {
        InnerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var length = InnerStream.Read(buffer, offset, count);
        OnRead?.Invoke(this, new(buffer, offset, length));
        return length;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return InnerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        InnerStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        OnWrite?.Invoke(this, new(buffer, offset, count));
        InnerStream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        InnerStream.Dispose();
    }

#if NET6_0_OR_GREATER
    public override ValueTask DisposeAsync() => InnerStream.DisposeAsync();
#endif
}

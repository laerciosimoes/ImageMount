﻿namespace ImageMounter.Devio.Interop.Client;

/// <summary>
/// Base class for classes that implement Stream for client side of Devio protocol.
/// </summary>
public abstract partial class DevioStream : Stream
{
    public event EventHandler Disposing;
    public event EventHandler Disposed;

    /// <summary>
    /// Object name used by proxy implementation.
    /// </summary>
    public string ObjectName { get; private set; }

    /// <summary>
    /// Virtual disk size of server object.
    /// </summary>
    protected long Size { get; set; }

    /// <summary>
    /// Alignment requirement for I/O at server.
    /// </summary>
    protected long Alignment { get; set; }

    /// <summary>
    /// Proxy flags specified for proxy connection.
    /// </summary>
    protected IMDPROXY_FLAGS Flags { get; set; }

    /// <summary>
    /// Initiates a new instance with supplied object name and read-only flag.
    /// </summary>
    /// <param name="name">Object name used by proxy implementation.</param>
    /// <param name="read_only">Flag set to true to indicate read-only proxy
    /// operation.</param>
    protected DevioStream(string name, bool read_only)
    {
        ObjectName = name;
        if (read_only)
        {
            Flags = IMDPROXY_FLAGS.IMDPROXY_FLAG_RO;
        }
    }

    /// <summary>
    /// Indicates whether Stream is readable. This implementation returns a
    /// constant value of True, because Devio proxy implementations are
    /// always readable.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Indicates whether Stream is seekable. This implementation returns a
    /// constant value of True.
    /// </summary>
    public override bool CanSeek => true;

    /// <summary>
    /// Indicates whether Stream is writable. This implementation returns True
    /// unless ProxyFlags property contains IMDPROXY_FLAGS.IMDPROXY_FLAG_RO value.
    /// </summary>
    public override bool CanWrite => !Flags.HasFlag(IMDPROXY_FLAGS.IMDPROXY_FLAG_RO);

    /// <summary>
    /// This implementation does not do anything.
    /// </summary>
    public override void Flush()
    {
    }

    /// <summary>
    /// When overridden in a derived class, closes communication and causes server side to exit.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        Disposing?.Invoke(this, EventArgs.Empty);
        base.Dispose(disposing);
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns current virtual disk size.
    /// </summary>
    public override long Length => Size;

    /// <summary>
    /// Current byte position in Stream.
    /// </summary>
    public override long Position { get; set; }

    /// <summary>
    /// Moves current position in Stream.
    /// </summary>
    /// <param name="offset">Byte offset to move. Can be negative to move backwards.</param>
    /// <param name="origin">Origin from where number of bytes to move counts.</param>
    /// <returns>Returns new absolute position in Stream.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                {
                    Position = offset;
                    break;
                }

            case SeekOrigin.Current:
                {
                    Position += offset;
                    break;
                }

            case SeekOrigin.End:
                {
                    Position = Size + offset;
                    break;
                }

            default:
                {
                    throw new ArgumentException("Invalid origin", nameof(origin));
                }
        }

        return Position;
    }

    /// <summary>
    /// This method is not supported in this implementation and throws a NotImplementedException.
    /// A derived class can override this method to implement a resize feature.
    /// </summary>
    /// <param name="value">New total size of Stream</param>
    public override void SetLength(long value) =>
        throw new NotImplementedException("SetLength() not implemented for DevioStream objects.");

    /// <summary>
    /// Alignment requirement for I/O at server.
    /// </summary>
    public long RequiredAlignment => Alignment;

    /// <summary>
    /// Proxy flags specified for proxy connection.
    /// </summary>
    public IMDPROXY_FLAGS ProxyFlags => Flags;

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) =>
        ((Task<int>)asyncResult).Result;

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer, offset, count));

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        WriteAsync(buffer, offset, count).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) => ((Task)asyncResult).Wait();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Read(buffer.Span));

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return new ValueTask();
    }
}

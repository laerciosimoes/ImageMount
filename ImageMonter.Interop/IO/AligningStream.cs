using System.Buffers;
using System.Diagnostics;

namespace ImageMounter.Interop.IO;

public class AligningStream : Stream
{
    private readonly bool _ownsBaseStream;

    public Stream BaseStream { get; }

    public int Alignment { get; }

    public AligningStream(Stream baseStream, int alignment, bool ownsBaseStream)
    {
        this.BaseStream = baseStream;
        this.Alignment = alignment;
        this._ownsBaseStream = ownsBaseStream;
    }

    public override bool CanRead => BaseStream.CanRead;

    public override bool CanSeek => BaseStream.CanSeek;

    public override bool CanWrite => BaseStream.CanWrite;

    public override long Length => BaseStream.Length;

    public override long Position { get; set; }

    public override void Flush() => BaseStream.Flush();

    private int SafeBaseRead(byte[] buffer, int offset, int count)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {count} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        else if (checked(Position + count > Length))
        {
            Trace.WriteLine($"Attempt to read {count} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            count = (int)(Length - Position);
        }

        var totalSize = 0;

        while (count > 0)
        {
            BaseStream.Position = Position;
            var blockSize = BaseStream.Read(buffer, offset, count);
            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            count -= blockSize;
            offset += blockSize;
            totalSize += blockSize;
        }

        return totalSize;
    }

    private async Task<int> SafeBaseReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {count} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        else if (checked(Position + count > Length))
        {
            Trace.WriteLine($"Attempt to read {count} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            count = (int)(Length - Position);
        }

        var totalSize = 0;

        while (count > 0)
        {
            BaseStream.Position = Position;
            var blockSize = await BaseStream.ReadAsync(buffer, offset, count, token).ConfigureAwait(false);
            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            count -= blockSize;
            offset += blockSize;
            totalSize += blockSize;
        }

        return totalSize;
    }


    private int SafeBaseRead(Span<byte> buffer)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        else if (checked(Position + buffer.Length > Length))
        {
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            buffer = buffer[..(int)(Length - Position)];
        }

        var totalSize = 0;

        while (buffer.Length > 0)
        {
            BaseStream.Position = Position;
            var blockSize = BaseStream.Read(buffer);
            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            buffer = buffer[blockSize..];
            totalSize += blockSize;
        }

        return totalSize;
    }

    private async ValueTask<int> SafeBaseReadAsync(Memory<byte> buffer, CancellationToken token)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        else if (checked(Position + buffer.Length > Length))
        {
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            buffer = buffer[..(int)(Length - Position)];
        }

        var totalSize = 0;

        while (buffer.Length > 0)
        {
            BaseStream.Position = Position;
            var blockSize = await BaseStream.ReadAsync(buffer, token).ConfigureAwait(false);
            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            buffer = buffer[blockSize..];
            totalSize += blockSize;
        }

        return totalSize;
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = buffer.Length;
        var offset = 0;

        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return await SafeBaseReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        using var newBufferHandle = MemoryPool<byte>.Shared.Rent(newsize);
        var newBuffer = newBufferHandle.Memory[..newsize];
        Position -= prefix;
        var result = await SafeBaseReadAsync(newBuffer, cancellationToken).ConfigureAwait(false);
        if (result < prefix)
        {
            return 0;
        }

        result -= prefix;

        if (result > count)
        {
            Position += count - result;
            result = count;
        }

        newBuffer.Slice(prefix, result).CopyTo(buffer[offset..]);

        return result;
    }

    public override int Read(Span<byte> buffer)
    {
        var count = buffer.Length;
        var offset = 0;

        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return SafeBaseRead(buffer);
        }

        using var newBufferHandle = MemoryPool<byte>.Shared.Rent(newsize);
        var newBuffer = newBufferHandle.Memory.Span[..newsize];
        Position -= prefix;
        var result = SafeBaseRead(newBuffer);
        if (result < prefix)
        {
            return 0;
        }

        result -= prefix;

        if (result > count)
        {
            Position += count - result;
            result = count;
        }

        newBuffer.Slice(prefix, result).CopyTo(buffer[offset..]);

        return result;
    }


    public override int Read(byte[] buffer, int offset, int count)
    {
        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return SafeBaseRead(buffer, offset, count);
        }

        var newbuffer = ArrayPool<byte>.Shared.Rent(newsize);
        try
        {
            Position -= prefix;
            var result = SafeBaseRead(newbuffer, 0, newsize);
            if (result < prefix)
            {
                return 0;
            }

            result -= prefix;

            if (result > count)
            {
                Position += count - result;
                result = count;
            }

            Buffer.BlockCopy(newbuffer, prefix, buffer, offset, result);

            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(newbuffer);
        }
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) => ((Task<int>)asyncResult).Result;

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return await SafeBaseReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        var newbuffer = ArrayPool<byte>.Shared.Rent(newsize);
        try
        {
            Position -= prefix;
            var result = await SafeBaseReadAsync(newbuffer, 0, newsize, cancellationToken).ConfigureAwait(false);
            if (result < prefix)
            {
                return 0;
            }

            result -= prefix;

            if (result > count)
            {
                Position += count - result;
                result = count;
            }

            Buffer.BlockCopy(newbuffer, prefix, buffer, offset, result);

            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(newbuffer);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => origin switch
    {
        SeekOrigin.Begin => Position = offset,
        SeekOrigin.Current => Position += offset,
        SeekOrigin.End => Position = Length + offset,
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    public override void Write(byte[] buffer, int offset, int count)
    {
        var originalPosition = Position;

        if (count == 0)
        {
            return;
        }

        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);

        var newArraySize = checked(prefix + count + suffix);

        var newBuffer = newArraySize != count ?
            ArrayPool<byte>.Shared.Rent(newArraySize) :
            null;

        try
        {
            if (newArraySize != count)
            {
                if (prefix != 0)
                {
                    Position = checked(originalPosition - prefix);

                    SafeBaseRead(newBuffer, 0, Alignment);
                }

                if (suffix != 0)
                {
                    Position = checked(originalPosition + count + suffix - Alignment);

                    SafeBaseRead(newBuffer, newArraySize - Alignment, Alignment);
                }

                Buffer.BlockCopy(buffer, offset, newBuffer, prefix, count);

                Position = originalPosition - prefix;

                buffer = newBuffer;
                offset = 0;
                count = newArraySize;
            }

            var absolute = Position + newArraySize;

            if (GrowInterval > 0 && absolute > Length)
            {
                absolute += (-absolute) & GrowInterval;

                SetLength(absolute);
            }

            BaseStream.Position = Position;
            BaseStream.Write(buffer, offset, count);
            Position = BaseStream.Position;

            if (suffix > 0)
            {
                Position -= suffix;
            }
        }
        finally
        {
            if (newBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(newBuffer);
            }
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) => ((Task)asyncResult).Wait();

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count == 0)
        {
            return;
        }

        var originalPosition = Position;

        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);

        var newArraySize = checked(prefix + count + suffix);

        Position -= prefix;

        var newBuffer = newArraySize != count ?
            ArrayPool<byte>.Shared.Rent(newArraySize) :
            null;

        try
        {
            if (count != newArraySize)
            {
                if (prefix != 0)
                {
                    Position = checked(originalPosition - prefix);

                    await SafeBaseReadAsync(newBuffer, 0, Alignment, cancellationToken).ConfigureAwait(false);
                }

                if (suffix != 0)
                {
                    Position = checked(originalPosition + count + suffix - Alignment);

                    await SafeBaseReadAsync(newBuffer, newArraySize - Alignment, Alignment, cancellationToken).ConfigureAwait(false);
                }

                Buffer.BlockCopy(buffer, offset, newBuffer, prefix, count);

                Position = originalPosition - prefix;

                buffer = newBuffer;
                offset = 0;
                count = newArraySize;
            }

            var absolute = Position + newArraySize;

            if (GrowInterval > 0 && absolute > Length)
            {
                absolute += (-absolute) & GrowInterval;

                SetLength(absolute);
            }

            BaseStream.Position = Position;
            await BaseStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            Position = BaseStream.Position;

            if (suffix > 0)
            {
                Position -= suffix;
            }
        }
        finally
        {
            if (newBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(newBuffer);
            }
        }
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        BaseStream.Position = Position;
        await BaseStream.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
        Position = BaseStream.Position;
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => BaseStream.FlushAsync(cancellationToken);


    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = buffer.Length;
        var offset = 0;

        if (count == 0)
        {
            return;
        }

        var originalPosition = Position;

        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);

        var newArraySize = checked(prefix + count + suffix);

        Position -= prefix;

        using var memoryOwner = count != newArraySize
            ? MemoryPool<byte>.Shared.Rent(newArraySize)
            : null;

        if (count != newArraySize)
        {
            var newBuffer = memoryOwner.Memory[..newArraySize];

            if (prefix != 0)
            {
                Position = checked(originalPosition - prefix);

                await ReadAsync(newBuffer[..Alignment], cancellationToken).ConfigureAwait(false);
            }

            if (suffix != 0)
            {
                Position = checked(originalPosition + count + suffix - Alignment);

                await ReadAsync(newBuffer[^Alignment..], cancellationToken).ConfigureAwait(false);
            }

            buffer.CopyTo(newBuffer[prefix..]);

            Position = originalPosition - prefix;

            buffer = newBuffer;
            offset = 0;
            count = newArraySize;
        }

        var absolute = Position + newArraySize;

        if (GrowInterval > 0 && absolute > Length)
        {
            absolute += (-absolute) & GrowInterval;

            SetLength(absolute);
        }

        BaseStream.Position = Position;
        await BaseStream.WriteAsync(buffer.Slice(offset, count), cancellationToken).ConfigureAwait(false);
        Position = BaseStream.Position;

        if (suffix > 0)
        {
            Position -= suffix;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var count = buffer.Length;
        var offset = 0;

        if (count == 0)
        {
            return;
        }

        var originalPosition = Position;

        var prefix = (int)(Position & (Alignment - 1));
        var suffix = -checked(prefix + count) & (Alignment - 1);

        var newArraySize = checked(prefix + count + suffix);

        Position -= prefix;

        using var memoryOwner = count != newArraySize
            ? MemoryPool<byte>.Shared.Rent(newArraySize)
            : null;

        if (count != newArraySize)
        {
            var newBuffer = memoryOwner.Memory[..newArraySize].Span;

            if (prefix != 0)
            {
                Position = checked(originalPosition - prefix);

                Read(newBuffer[..Alignment]);
            }

            if (suffix != 0)
            {
                Position = checked(originalPosition + count + suffix - Alignment);

                Read(newBuffer[^Alignment..]);
            }

            buffer.CopyTo(newBuffer[prefix..]);

            Position = originalPosition - prefix;

            buffer = newBuffer;
            offset = 0;
            count = newArraySize;
        }

        var absolute = Position + newArraySize;

        if (GrowInterval > 0 && absolute > Length)
        {
            absolute += (-absolute) & GrowInterval;

            SetLength(absolute);
        }

        BaseStream.Position = Position;
        BaseStream.Write(buffer.Slice(offset, count));
        Position = BaseStream.Position;

        if (suffix > 0)
        {
            Position -= suffix;
        }
    }


    public override bool CanTimeout => BaseStream.CanTimeout;

    protected override void Dispose(bool disposing)
    {
        if (disposing && _ownsBaseStream)
        {
            BaseStream.Dispose();
        }

        base.Dispose(disposing);
    }

    public override int ReadTimeout
    {
        get => BaseStream.ReadTimeout;
        set => BaseStream.ReadTimeout = value;
    }

    public override void SetLength(long value) => BaseStream.SetLength(value);

    public override int WriteTimeout
    {
        get => BaseStream.WriteTimeout;
        set => BaseStream.WriteTimeout = value;
    }

    public long GrowInterval { get; set; } = (32L << 20) - 1;
}

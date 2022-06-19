
namespace ImageMounter.Interop.IO;

public class SubStream : Stream
{
    private readonly long _length;

    public bool OwnsParent { get; }
    public long Start { get; }
    public Stream Parent { get; }

    public SubStream(Stream parent, long start, long length)
    {
        Parent = parent;
        Start = start;
        _length = length;
        OwnsParent = false;

        if (checked(Start + _length) > Parent.Length)
        {
            throw new ArgumentException("SubStream extends beyond end of parent stream");
        }
    }

    public SubStream(Stream parent, bool ownsParent, long start, long length)
    {
        Parent = parent;
        OwnsParent = ownsParent;
        Start = start;
        _length = length;

        if (checked(Start + _length) > Parent.Length)
        {
            throw new ArgumentException("SubStream extends beyond end of parent stream");
        }
    }

    public override bool CanRead => Parent.CanRead;

    public override bool CanSeek => Parent.CanSeek;

    public override bool CanWrite => Parent.CanWrite;

    public override long Length => _length;

    public override long Position
    {
        get => checked(Parent.Position - Start);

        set
        {
            if (value >= 0 && value <= _length)
            {
                Parent.Position = checked(value + Start);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Attempt to move beyond start or end of stream");
            }
        }
    }

    public override void Flush() => Parent.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => Parent.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        if (Position >= _length)
        {
            return 0;
        }

        return Parent.Read(buffer, offset, (int)Math.Min(count, checked(_length - Position)));
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        if (Position >= _length)
        {
            return LowLevelExtensions.ZeroCompletedTask;
        }

        return Parent.ReadAsync(buffer, offset, (int)Math.Min(count, checked(_length - Position)), cancellationToken);
    }


    public override int Read(Span<byte> buffer)
    {
        if (Position >= _length)
        {
            return 0;
        }

        return Parent.Read(buffer[..(int)Math.Min(buffer.Length, checked(_length - Position))]);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Position >= _length)
        {
            return new ValueTask<int>(0);
        }

        return Parent.ReadAsync(buffer[..(int)Math.Min(buffer.Length, checked(_length - Position))], cancellationToken);
    }


    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        if (Position >= _length)
        {
            count = 0;
        }

        return Parent.BeginRead(buffer, offset, (int)Math.Min(count, checked(_length - Position)), callback, state);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Current)
        {
            Position += offset;
        }
        else if (origin == SeekOrigin.End)
        {
            Position = _length + offset;
        }
        else if (origin == SeekOrigin.Begin)
        {
            Position = offset;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "Invalid origin");
        }

        return Position;
    }

    public override void SetLength(long value) => throw new NotSupportedException("Attempt to change length of a SubStream");

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        if (checked(Position + count) > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of SubStream");
        }

        Parent.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        if (checked(Position + count) > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of SubStream");
        }

        return Parent.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (checked(Position + buffer.Length) > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer), "Attempt to write beyond end of SubStream");
        }

        Parent.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (checked(Position + buffer.Length) > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer), "Attempt to write beyond end of SubStream");
        }

        return Parent.WriteAsync(buffer, cancellationToken);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        if (checked(Position + count) > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of SubStream");
        }

        return Parent.BeginWrite(buffer, offset, count, callback, state);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (OwnsParent)
                {
                    Parent.Dispose();
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}

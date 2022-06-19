
namespace ImageMounter.Interop.IO;

public class CombinedSeekStream : Stream
{
    private readonly Dictionary<long, Stream> _streams;

    private KeyValuePair<long, Stream> _current;

    public bool Extendable { get; }

    public ICollection<Stream> BaseStreams => _streams.Values;

    public Stream CurrentBaseStream => _current.Value;

    public CombinedSeekStream()
        : this(true)
    {
    }

    public CombinedSeekStream(params Stream[] inputStreams)
        : this(false, inputStreams)
    {
    }

    public CombinedSeekStream(bool writable, params Stream[] inputStreams)
    {
        if (inputStreams.Length == 0)
        {
            _streams = new();

            Extendable = true;
        }
        else
        {
            _streams = new(inputStreams.Length);

            Array.ForEach(inputStreams, AddStream);

            Seek(0, SeekOrigin.Begin);
        }

        CanWrite = writable;
    }

    public void AddStream(Stream stream)
    {
        if (!stream.CanSeek || !stream.CanRead)
        {
            throw new NotSupportedException("Needs seekable and readable streams");
        }

        if (stream.Length == 0)
        {
            return;
        }

        checked
        {
            _length += stream.Length;
        }

        _streams.Add(_length, stream);
    }

    public override int Read(byte[] buffer, int index, int count)
    {
        var num = 0;

        while (count > 0)
        {
            var r = _current.Value.Read(buffer, index, count);

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        var num = 0;

        while (count > 0)
        {
            var r = await _current.Value.ReadAsync(buffer, index, count, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) =>
        ((Task<int>)asyncResult).Result;


    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = buffer.Length;
        var index = 0;
        var num = 0;

        while (count > 0)
        {
            var r = await _current.Value.ReadAsync(buffer.Slice(index, count), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

    public override int Read(Span<byte> buffer)
    {
        var count = buffer.Length;
        var index = 0;
        var num = 0;

        while (count > 0)
        {
            var r = _current.Value.Read(buffer.Slice(index, count));

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }


    public override void Write(byte[] buffer, int index, int count)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer, index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (count > 0)
        {
            var currentCount = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            _current.Value.Write(buffer, index, currentCount);

            Seek(currentCount, SeekOrigin.Current);

            index += currentCount;
            count -= currentCount;
        }
    }

    public override async Task WriteAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer, index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (_current.Value is not null && count > 0)
        {
            var currentCount = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            await _current.Value.WriteAsync(buffer, index, currentCount, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Seek(currentCount, SeekOrigin.Current);

            index += currentCount;
            count -= currentCount;
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) =>
        ((Task)asyncResult).Wait();


    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        var count = buffer.Length;
        var index = 0;

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer.ToArray(), index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (count > 0)
        {
            var currentCount = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            await _current.Value.WriteAsync(buffer.Slice(index, currentCount), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Seek(currentCount, SeekOrigin.Current);

            index += currentCount;
            count -= currentCount;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        var count = buffer.Length;
        var index = 0;

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer.ToArray(), index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (count > 0)
        {
            var currentCount = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            _current.Value.Write(buffer.Slice(index, currentCount));

            Seek(currentCount, SeekOrigin.Current);

            index += currentCount;
            count -= currentCount;
        }
    }

    public override void Flush() => _current.Value?.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _current.Value?.FlushAsync(cancellationToken) ?? Task.FromResult(0);

    private long _position;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public long? PhysicalPosition
    {
        get
        {
            var stream = _current.Value;

            return stream.Position;
        }
    }

    private long _length;

    public override long Length => _length;

    public override void SetLength(long value) => throw new NotSupportedException();

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite { get; }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Current:
                offset += _position;
                break;

            case SeekOrigin.End:
                offset = Length + offset;
                break;
            case SeekOrigin.Begin:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        if (offset < 0)
        {
            throw new ArgumentException("Negative stream positions not supported");
        }

        _current = _streams.FirstOrDefault(s => s.Key > offset);

        _current.Value.Position = _current.Value.Length - (_current.Key - offset);

        _position = offset;

        return offset;
    }

    public override void Close()
    {
        _streams.Values.AsParallel().ForAll(stream => stream.Dispose());
        _streams.Clear();

        base.Close();
    }
}

using ImageMounter.Devio.Interop.Server.GenericProviders;


namespace ImageMounter.Devio.Interop.Client;

/// <summary>
/// Base class for classes that implement Stream for client side of Devio protocol.
/// </summary>
public partial class DevioDirectStream : DevioStream
{
    public IDevioProvider Provider { get; private set; }
    public bool OwnsProvider { get; private set; }

    /// <summary>
    /// Initiates a new instance with supplied provider object.
    /// </summary>
    public DevioDirectStream(IDevioProvider provider, bool ownsProvider) :
        base(provider.NullCheck(nameof(provider)).ToString(), !provider.CanWrite)
    {
        Provider = provider;
        OwnsProvider = ownsProvider;
        Size = provider.Length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesread = Provider.Read(buffer, offset, count, Position);
        if (bytesread > 0)
        {
            Position += bytesread;
        }

        return bytesread;
    }

    public unsafe override int Read(Span<byte> buffer)
    {
        fixed (void* ptr = buffer)
        {
            var bytesread = Provider.Read(new IntPtr(ptr), 0, buffer.Length, Position);

            if (bytesread > 0)
            {
                Position += bytesread;
            }

            return bytesread;
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var byteswritten = Provider.Write(buffer, offset, count, Position);
        if (byteswritten > 0)
        {
            Position += byteswritten;
        }

        if (byteswritten != count)
        {
            if (byteswritten > 0)
            {
                throw new IOException("Not all data were written");
            }
            else
            {
                throw new IOException("Write error");
            }
        }
    }

    public unsafe override void Write(ReadOnlySpan<byte> buffer)
    {
        fixed (void* ptr = buffer)
        {
            var byteswritten = Provider.Write(new IntPtr(ptr), 0, buffer.Length, Position);
            if (byteswritten > 0)
            {
                Position += byteswritten;
            }

            if (byteswritten != buffer.Length)
            {
                if (byteswritten > 0)
                {
                    throw new IOException("Not all data were written");
                }
                else
                {
                    throw new IOException("Write error");
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (OwnsProvider)
        {
            Provider?.Dispose();
        }

        base.Dispose(disposing);
    }
}

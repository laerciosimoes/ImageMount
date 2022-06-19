using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static ImageMounter.Devio.Interop.IMDPROXY_CONSTANTS;


namespace ImageMounter.Devio.Interop.Client;

/// <summary>
/// Derives DevioStream and implements client side of Devio shared memory communication proxy.
/// </summary>
public partial class DevioShmStream : DevioStream
{
    private readonly EventWaitHandle RequestEvent;
    private readonly EventWaitHandle ResponseEvent;
    private readonly Mutex ServerMutex;
    private readonly SafeBuffer MapView;

    /// <summary>
    /// Creates a new instance by opening an existing Devio shared memory object and starts
    /// communication with a Devio service using this shared memory object.
    /// </summary>
    /// <param name="name">Name of shared memory object to use for communication.</param>
    /// <param name="read_only">Specifies if communication should be read-only.</param>
    /// <returns>Returns new instance of DevioShmStream.</returns>
    public static DevioShmStream Open(string name, bool read_only) => new(name, read_only);

    /// <summary>
    /// Creates a new instance by opening an existing Devio shared memory object and starts
    /// communication with a Devio service using this shared memory object.
    /// </summary>
    /// <param name="name">Name of shared memory object to use for communication.</param>
    /// <param name="read_only">Specifies if communication should be read-only.</param>
    public DevioShmStream(string name, bool read_only) : base(name, read_only)
    {
        try
        {
            using (var Mapping = MemoryMappedFile.OpenExisting(ObjectName, MemoryMappedFileRights.ReadWrite))
            {
                MapView = Mapping.CreateViewAccessor().SafeMemoryMappedViewHandle;
            }

            RequestEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Request");
            ResponseEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Response");
            ServerMutex = new Mutex(initiallyOwned: false, name: $@"Global\{ObjectName}_Server");
            MapView.Write(0x0, IMDPROXY_REQ.IMDPROXY_REQ_INFO);
            RequestEvent.Set();
            if (WaitHandle.WaitAny(new WaitHandle[] { ResponseEvent, ServerMutex }) != 0)
            {
                throw new EndOfStreamException("Server exit.");
            }

            var response = MapView.Read<IMDPROXY_INFO_RESP>(0x0UL);
            Size = (long)response.file_size;
            Alignment = (long)response.req_alignment;
            Flags |= response.flags;
        }
        catch (Exception ex)
        {
            Dispose();
            throw new Exception("Error initializing stream based shared memory proxy", ex);
        }
    }

    public override void Close()
    {
        try
        {
            MapView.Write(0x0, IMDPROXY_REQ.IMDPROXY_REQ_CLOSE);
            RequestEvent.Set();
        }
        finally
        {
            base.Close();
            foreach (var obj in new IDisposable[] { ServerMutex, MapView, RequestEvent, ResponseEvent })
            {
                obj?.Dispose();
            }
        }

    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var Request = default(IMDPROXY_READ_REQ);
        Request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_READ;
        Request.offset = (ulong)Position;
        Request.length = (ulong)count;
        MapView.Write(0x0, Request);
        RequestEvent.Set();
        if (WaitHandle.WaitAny(new WaitHandle[] { ResponseEvent, ServerMutex }) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var Response = MapView.Read<IMDPROXY_READ_RESP>(0x0UL);
        if (Response.errorno != 0)
        {
            throw new EndOfStreamException($"Read error: {Response.errorno}");
        }

        var Length = (int)Response.length;
        MapView.ReadArray(IMDPROXY_HEADER_SIZE, buffer, offset, Length);
        Position += Length;
        return Length;
    }

    public override unsafe int Read(Span<byte> buffer)
    {
        var request = default(IMDPROXY_READ_REQ);
        request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_READ;
        request.offset = (ulong)Position;
        request.length = (ulong)buffer.Length;
        MapView.Write(0x0, request);
        RequestEvent.Set();
        if (WaitHandle.WaitAny(new WaitHandle[] { ResponseEvent, ServerMutex }) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var response = MapView.Read<IMDPROXY_READ_RESP>(0x0UL);
        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Read error: {response.errorno}");
        }

        var length = (int)response.length;
        fixed (void* bufptr = buffer)
        {
            Buffer.MemoryCopy((MapView.DangerousGetHandle() + IMDPROXY_HEADER_SIZE).ToPointer(), bufptr, buffer.Length, length);
        }
        Position += length;
        return length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var request = default(IMDPROXY_WRITE_REQ);
        request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_WRITE;
        request.offset = (ulong)Position;
        request.length = (ulong)count;
        MapView.Write(0x0, request);
        MapView.WriteArray(IMDPROXY_HEADER_SIZE, buffer, offset, count);
        RequestEvent.Set();
        if (WaitHandle.WaitAny(new WaitHandle[] { ResponseEvent, ServerMutex }) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var response = MapView.Read<IMDPROXY_WRITE_RESP>(0x0UL);
        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Write error: {response.errorno}");
        }

        var length = (int)response.length;
        Position += length;
        if (length != count)
        {
            throw new EndOfStreamException($"Write length mismatch. Wrote {length} of {count} bytes.");
        }
    }

    public override unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        var request = default(IMDPROXY_WRITE_REQ);
        request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_WRITE;
        request.offset = (ulong)Position;
        request.length = (ulong)buffer.Length;
        MapView.Write(0x0, request);
        fixed (void* bufptr = buffer)
        {
            Buffer.MemoryCopy(bufptr, (MapView.DangerousGetHandle() + IMDPROXY_HEADER_SIZE).ToPointer(), (long)(MapView.ByteLength - IMDPROXY_HEADER_SIZE), buffer.Length);
        }
        RequestEvent.Set();
        if (WaitHandle.WaitAny(new WaitHandle[] { ResponseEvent, ServerMutex }) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var response = MapView.Read<IMDPROXY_WRITE_RESP>(0x0UL);
        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Write error: {response.errorno}");
        }

        var i = (int)response.length;
        Position += i;
        if (i != buffer.Length)
        {
            throw new EndOfStreamException($"Write length mismatch. Wrote {i} of {buffer.Length} bytes.");
        }
    }
}

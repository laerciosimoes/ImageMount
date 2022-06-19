using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Runtime.InteropServices;

namespace Server.GenericProviders;

/// <summary>
/// Base class for implementing <see>IDevioProvider</see> interface with a storage backend where
/// bytes to read from and write to device are provided in an unmanaged memory area.
/// </summary>
public abstract class DevioProviderUnmanagedBase : IDevioProvider
{

    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler Disposed;

    /// <summary>
    /// Determines whether virtual disk is writable or read-only.
    /// </summary>
    /// <value>True if virtual disk can be written to through this instance, or False
    /// if it is opened for reading only.</value>
    /// <returns>True if virtual disk can be written to through this instance, or False
    /// if it is opened for reading only.</returns>
    public abstract bool CanWrite { get; }

    /// <summary>
    /// Indicates whether provider supports shared image operations with registrations
    /// and reservations.
    /// </summary>
    public virtual bool SupportsShared { get; }

    /// <summary>
    /// Size of virtual disk.
    /// </summary>
    /// <value>Size of virtual disk.</value>
    /// <returns>Size of virtual disk.</returns>
    public abstract long Length { get; }

    /// <summary>
    /// Sector size of virtual disk.
    /// </summary>
    /// <value>Sector size of virtual disk.</value>
    /// <returns>Sector size of virtual disk.</returns>
    public abstract uint SectorSize { get; }

    private int Read(byte[] buffer, int bufferoffset, int count, long fileoffset)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        else if (bufferoffset + count > buffer.Length)
            throw new ArgumentException("buffer too small");

        var pinptr = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Read(pinptr.AddrOfPinnedObject(), bufferoffset, count, fileoffset);
        }
        finally
        {
            pinptr.Free();
        }
    }

    /// <summary>
    /// Reads bytes from virtual disk to a memory area specified by a pointer to unmanaged memory.
    /// </summary>
    /// <param name="buffer">Pointer to unmanaged memory where read bytes are stored.</param>
    /// <param name="bufferoffset">Offset in unmanaged memory buffer where bytes are stored.</param>
    /// <param name="count">Number of bytes to read from virtual disk device.</param>
    /// <param name="fileoffset">Offset at virtual disk device where read starts.</param>
    /// <returns>Returns number of bytes read from device that were stored at specified memory position.</returns>
    public abstract int Read(IntPtr buffer, int bufferoffset, int count, long fileoffset);

    private int Write(byte[] buffer, int bufferoffset, int count, long fileoffset)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        else if (bufferoffset + count > buffer.Length)
            throw new ArgumentException("buffer too small");

        var pinptr = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Write(pinptr.AddrOfPinnedObject(), bufferoffset, count, fileoffset);
        }
        finally
        {
            pinptr.Free();
        }
    }

    /// <summary>
    /// Writes out bytes to virtual disk device from a memory area specified by a pointer to unmanaged memory.
    /// </summary>
    /// <param name="buffer">Pointer to unmanaged memory area containing bytes to write out to device.</param>
    /// <param name="bufferoffset">Offset in unmanaged memory buffer where bytes to write are located.</param>
    /// <param name="count">Number of bytes to write to virtual disk device.</param>
    /// <param name="fileoffset">Offset at virtual disk device where write starts.</param>
    /// <returns>Returns number of bytes written to device.</returns>
    public abstract int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset);

    /// <summary>
    /// Manage registrations and reservation keys for shared images.
    /// </summary>
    /// <param name="Request">Request data</param>
    /// <param name="Response">Response data</param>
    /// <param name="Keys">List of currently registered keys</param>
    public virtual void SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
    {
        throw new NotImplementedException();
    }

    public bool IsDisposed { get; } // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        OnDisposing(EventArgs.Empty);

        if (!IsDisposed)
        {
            if (disposing)
            {
            }
        }
        IsDisposed = true;

        OnDisposed(EventArgs.Empty);
    }

    // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
    ~DevioProviderUnmanagedBase()
    {
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(false);
        base.Finalize();
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Raises Disposing event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposing(EventArgs e)
    {
        Disposing?.Invoke(this, e);
    }

    /// <summary>
    /// Raises Disposed event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposed(EventArgs e)
    {
        Disposed?.Invoke(this, e);
    }
}
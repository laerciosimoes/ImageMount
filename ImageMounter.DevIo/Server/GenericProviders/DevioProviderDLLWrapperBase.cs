using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Server.GenericProviders;

namespace ImageMounter.DevIo.Server.GenericProviders;

/// <summary>
/// Class that implements <see>IDevioProvider</see> interface with an unmanaged DLL
/// written for use with devio.exe command line tool.
/// object as storage backend.
/// </summary>
public abstract class DevioProviderDLLWrapperBase : DevioProviderUnmanagedBase
{
    public class SafeDevioProviderDLLHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        protected internal DLLCloseMethod DLLClose { get; set; }

        public SafeDevioProviderDLLHandle(IntPtr handle, bool ownsHandle) : base(ownsHandle)
        {
            SafeHandle.SetHandle(handle);
        }

        protected SafeDevioProviderDLLHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            if (DLLClose == null)
                return true;
            return DLLClose(SafeHandle.handle) != 0;
        }
    }

    protected DevioProviderDLLWrapperBase(DLLOpenMethod open, string filename, bool readOnly) : this(open, filename, readOnly, null)
    {
    }

    protected DevioProviderDLLWrapperBase(DLLOpenMethod open, string filename, bool readOnly, Func<Exception> get_last_error)
    {
        if (open == null)
            throw new ArgumentNullException(nameof(open));

        DLLCloseMethod dll_close = null;

        SafeHandle = open(filename, readOnly, out DLLRead, out DLLWrite, out dll_close, out Length);

        if (SafeHandle.IsInvalid || SafeHandle.IsClosed)
            throw new IOException($"Error opening '{filename}'", get_last_error?.Invoke() ?? new Win32Exception());

        SafeHandle.DLLClose = dll_close;

        CanWrite = !readOnly;
    }

    public SafeDevioProviderDLLHandle SafeHandle { get; }

    public override long Length { get; }

    public override bool CanWrite { get; }

    public virtual DLLReadWriteMethod DLLRead { get; }

    public virtual DLLReadWriteMethod DLLWrite { get; }

    public delegate SafeDevioProviderDLLHandle DLLOpenMethod([MarshalAs(UnmanagedType.LPStr)][In] string filename, [MarshalAs(UnmanagedType.Bool)] bool read_only, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllread, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLReadWriteMethod dllwrite, [MarshalAs(UnmanagedType.FunctionPtr)] out DLLCloseMethod dllclose, out long size);

    public delegate int DLLReadWriteMethod(SafeDevioProviderDLLHandle handle, IntPtr buffer, int size, long offset);

    public delegate int DLLCloseMethod(IntPtr handle);

    public override int Read(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {
        return DLLRead(SafeHandle, buffer + bufferoffset, count, fileoffset);
    }

    public override int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {
        return DLLWrite(SafeHandle, buffer + bufferoffset, count, fileoffset);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            SafeHandle?.Dispose();

        SafeHandle = null;

        base.Dispose(disposing);
    }
}
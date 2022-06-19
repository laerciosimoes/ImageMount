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
using System.Runtime.Versioning;
using Arsenal.ImageMounter.IO;
using ImageMounter;
using ImageMounter.IO;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter
{
    /// <summary>

    /// ''' Base class that represents Arsenal Image Mounter SCSI miniport created device objects.

    /// ''' </summary>
    public abstract class DeviceObject : IDisposable
    {
        public SafeFileHandle SafeFileHandle { get; }

        public FileAccess AccessMode { get; }

        /// <summary>
        /// Opens specified Path with CreateFile Win32 API and encapsulates the returned handle
        /// in a new DeviceObject.
        /// </summary>
        /// <param name="Path">Path to pass to CreateFile API</param>
        protected DeviceObject(string Path) : this(
            NativeFileIO.OpenFileHandle(Path, 0, FileShare.ReadWrite, FileMode.Open, Overlapped: false), 0)
        {
        }

        /// <summary>
        /// Opens specified Path with CreateFile Win32 API and encapsulates the returned handle
        /// in a new DeviceObject.
        /// </summary>
        /// <param name="Path">Path to pass to CreateFile API</param>
        /// <param name="AccessMode">Access mode for opening and for underlying FileStream</param>
        protected DeviceObject(string Path, FileAccess AccessMode) : this(
            NativeFileIO.OpenFileHandle(Path, AccessMode, FileShare.ReadWrite, FileMode.Open, Overlapped: false),
            AccessMode)
        {
        }

        /// <summary>
        /// Encapsulates a handle in a new DeviceObject.
        /// </summary>
        /// <param name="Handle">Existing handle to use</param>
        /// <param name="Access">Access mode for underlying FileStream</param>
        protected DeviceObject(SafeFileHandle Handle, FileAccess Access)
        {
            SafeFileHandle = Handle;
            AccessMode = Access;
        }

        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                    // TODO: dispose managed state (managed objects).
                    SafeFileHandle?.Dispose();

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                // TODO: set large fields to null.
                SafeFileHandle = null;
            }

            this.disposedValue = true;
        }

        // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        ~DeviceObject()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(false);
            base.Finalize();
        }

        /// <summary>
        /// Close device object.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
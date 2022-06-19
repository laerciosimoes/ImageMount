using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{

    /// <summary>
    ///  Encapsulates a Service Control Management object handle that is closed by calling CloseServiceHandle() Win32 API.
    ///  </summary>
    public sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [System.Runtime.InteropServices.DllImport("advapi32")]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeServiceHandle(IntPtr open_handle, bool owns_handle) : base(owns_handle)
        {
            System.Runtime.InteropServices.SafeHandle.SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeServiceHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle()
        {
            return CloseServiceHandle(System.Runtime.InteropServices.SafeHandle.handle);
        }
    }
}

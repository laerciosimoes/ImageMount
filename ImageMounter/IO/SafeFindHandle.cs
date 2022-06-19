using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{
    /// <summary>
    /// Encapsulates a FindVolumeMountPoint handle that is closed by calling FindVolumeMountPointClose () Win32 API.
    /// </summary>
    public sealed class SafeFindHandle : SafeHandleMinusOneIsInvalid
    {
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern bool FindClose(IntPtr h);

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        [SecurityCritical]
        public SafeFindHandle(IntPtr open_handle, bool owns_handle) : base(owns_handle)
        {
            SafeHandle.SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        [SecurityCritical]
        public SafeFindHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// Closes contained handle by calling FindClose() Win32 API.
        /// </summary>
        /// <returns>Return value from FindClose() Win32 API.</returns>
        [SecurityCritical]
        protected override bool ReleaseHandle()
        {
            return FindClose(SafeHandle.handle);
        }
    }

}

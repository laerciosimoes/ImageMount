using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{
    /// <summary>
    ///  Encapsulates a SetupAPI hInf handle that is closed by calling SetupCloseInf() API.
    ///  </summary>
    public sealed class SafeDeviceInfoSetHandle : SafeHandleMinusOneIsInvalid
    {
        [System.Runtime.InteropServices.DllImport("setupapi")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr handle);

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeDeviceInfoSetHandle(IntPtr open_handle, bool owns_handle) : base(owns_handle)
        {
            SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeDeviceInfoSetHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle()
        {
            return SetupDiDestroyDeviceInfoList(handle);
        }
    }


}

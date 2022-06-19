using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{
    /// <summary>
    ///  Encapsulates a FindVolume handle that is closed by calling FindVolumeClose() Win32 API.
    ///  </summary>
    public sealed class SafeFindVolumeHandle : SafeHandleMinusOneIsInvalid
    {
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern bool FindVolumeClose(IntPtr h);

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="openHandle">Existing open handle.</param>
        /// <param name="ownsHandle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeFindVolumeHandle(IntPtr openHandle, bool ownsHandle) : base(ownsHandle)
        {
            System.Runtime.InteropServices.SafeHandle.SetHandle(openHandle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeFindVolumeHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle()
        {
            return FindVolumeClose(System.Runtime.InteropServices.SafeHandle.handle);
        }
    }


}

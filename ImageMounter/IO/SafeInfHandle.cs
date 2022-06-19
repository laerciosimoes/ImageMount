using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{
    /// <summary>
    /// Encapsulates a SetupAPI hInf handle that is closed by calling SetupCloseInf() API.
    /// </summary>
    public sealed class SafeInfHandle : SafeHandleMinusOneIsInvalid
    {

        [System.Runtime.InteropServices.DllImport("setupapi")]
        private static extern void SetupCloseInfFile(IntPtr hInf);

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="openHandle">Existing open handle.</param>
        /// <param name="ownsHandle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeInfHandle(IntPtr openHandle, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(openHandle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeInfHandle() : base(true)
        {

        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle()
        {
            SetupCloseInfFile(handle);
            return true;
        }
    }
}
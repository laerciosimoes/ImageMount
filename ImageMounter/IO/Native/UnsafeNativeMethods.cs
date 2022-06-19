using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.IO.Native.Enum;
using ImageMounter.IO.Native.Struct;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO.Native
{

    public sealed class UnsafeNativeMethods
    {
        [DllImport("kernel32")]
        internal static extern bool DuplicateHandle(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle,
            out SafeWaitHandle lpTargetHandle,
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwOptions);


        [DllImport("kernel32")]
        internal static extern bool SetEvent(SafeWaitHandle hEvent);

        [DllImport("kernel32")]
        internal static extern bool SetHandleInformation(SafeWaitHandle h, uint mask, ref uint flags);



        [DllImport("ntdll")]
        internal static extern Int32 NtCreateFile(out IntPtr hFile,
            FileSystemRights AccessMask,
            ObjectAttributes ObjectAttributes,
            out IoStatusBlock IoStatusBlock,
            long AllocationSize,
            FileAttributes FileAttributes,
            FileShare ShareAccess,
            NtCreateDisposition CreateDisposition,
            NtCreateOptions CreateOptions,
            IntPtr EaBuffer,
            UInt32 EaLength);

        [DllImport("ntdll")]
        internal static extern Int32 NtOpenEvent(out IntPtr hEvent, uint AccessMask,
            ref ObjectAttributes ObjectAttributes);

        [DllImport("kernel32")]
        internal static extern bool GetFileInformationByHandle(IntPtr hFile,
            out ByHandleFileInformation lpFileInformation);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileTime(IntPtr hFile, out long lpCreationTime, out long lpLastAccessTime,
            out long lpLastWriteTime);

        [DllImport("ntdll")]
        internal static extern Int32 RtlNtStatusToDosError(Int32 NtStatus);

    }

}

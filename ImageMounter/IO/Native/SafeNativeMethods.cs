using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.IO.Native
{
    public sealed class SafeNativeMethods
    {
        [DllImport("kernel32")]
        public static extern bool AllocConsole();

        [DllImport("kernel32")]
        public static extern bool FreeConsole();

        [DllImport("kernel32")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32")]
        public static extern uint GetLogicalDrives();

        [DllImport("kernel32")]
        public static extern FileAttributes GetFileAttributes(
            [MarshalAs(UnmanagedType.LPWStr)][In] string lpFileName
        );

        [DllImport("kernel32")]
        public static extern bool SetFileAttributes([MarshalAs(UnmanagedType.LPWStr)][In] string lpFileName,
            FileAttributes dwFileAttributes
        );

        [DllImport("kernel32")]
        public static extern long GetTickCount64();
    }

}

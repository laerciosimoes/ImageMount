using System.Runtime.InteropServices;
using ImageMounter.IO;
using ImageMounter.IO.Native;

namespace ImageMounter.Reflection
{
    public abstract class NativeLib
    {
        private NativeLib()
        {
        }

        /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
        public static bool IsWindows { get; } = true;

        public static Delegate GetProcAddress(IntPtr hModule, string procedureName, Type delegateType)
        {
            return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType);
        }

        public static Delegate GetProcAddressNoThrow(IntPtr hModule, string procedureName, Type delegateType)
        {
            var fptr = UnsafeNativeMethods.GetProcAddress(hModule, procedureName);

            if (fptr == null/* TODO Change to default(_) if this is not a reference type */ )
                return null;

            return Marshal.GetDelegateForFunctionPointer(fptr, delegateType);
        }

        public static Delegate GetProcAddress(string moduleName, string procedureName, Type delegateType)
        {
            var hModule = Win32Try(UnsafeNativeMethods.LoadLibrary(moduleName));

            return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType);
        }

        public static IntPtr GetProcAddressNoThrow(string moduleName, string procedureName)
        {
            var hModule = UnsafeNativeMethods.LoadLibrary(moduleName);

            if (hModule == null/* TODO Change to default(_) if this is not a reference type */ )
                return default(IntPtr);

            return UnsafeNativeMethods.GetProcAddress(hModule, procedureName);
        }

        /* TODO ERROR: Skipped EndIfDirectiveTrivia */
        public static Delegate GetProcAddressNoThrow(string moduleName, string procedureName, Type delegateType)
        {
            var fptr = GetProcAddressNoThrow(moduleName, procedureName);

            if (fptr == default(IntPtr))
                return null;

            return Marshal.GetDelegateForFunctionPointer(fptr, delegateType);
        }
    }
}

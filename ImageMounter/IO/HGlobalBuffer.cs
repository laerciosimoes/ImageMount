using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.IO
{
    public class HGlobalBuffer : SafeBuffer
    {
        public HGlobalBuffer(IntPtr numBytes) : base(ownsHandle: true)
        {
            var ptr = Marshal.AllocHGlobal(numBytes);
            base.SetHandle(ptr);
            base.Initialize(System.Convert.ToUInt64(numBytes));
        }

        public HGlobalBuffer(int numBytes) : base(ownsHandle: true)
        {
            var ptr = Marshal.AllocHGlobal(numBytes);
            base.SetHandle(ptr);
            base.Initialize(System.Convert.ToUInt64(numBytes));
        }

        public HGlobalBuffer(IntPtr address, ulong numBytes, bool ownsHandle) : base(ownsHandle)
        {
            base.SetHandle(address);
            base.Initialize(numBytes);
        }

        public void Resize(int newSize)
        {
            if (SafeHandle.handle != IntPtr.Zero)
                Marshal.FreeHGlobal(SafeHandle.handle);
            SafeHandle.handle = Marshal.AllocHGlobal(newSize);
            base.Initialize(System.Convert.ToUInt64(newSize));
        }

        public void Resize(IntPtr newSize)
        {
            if (SafeHandle.handle != IntPtr.Zero)
                Marshal.FreeHGlobal(SafeHandle.handle);
            SafeHandle.handle = Marshal.AllocHGlobal(newSize);
            base.Initialize(System.Convert.ToUInt64(newSize));
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                Marshal.FreeHGlobal(SafeHandle.handle);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

}

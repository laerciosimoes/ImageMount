using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{
    internal sealed class NativeWaitHandle : WaitHandle
    {
        public NativeWaitHandle(SafeWaitHandle handle)
        {
            WaitHandle.SafeWaitHandle = handle;
        }
    }
}

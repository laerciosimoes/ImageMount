using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.DevIo.Server.Services
{

    public interface IVirtualDiskService : IDisposable
    {

        event EventHandler ServiceShutdown;

        event EventHandler ServiceStopping;

        event ThreadExceptionEventHandler ServiceUnhandledException;

        bool IsDisposed { get; }

        bool HasDiskDevice { get; }
        uint SectorSize { get; }

        long DiskSize { get; }

        string Description { get; }

        string GetDiskDeviceName();

        void RemoveDevice();

        void RemoveDeviceSafe();

        void WaitForServiceThreadExit();
    }
}



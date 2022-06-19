using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Runtime.Versioning;
using DiscUtils;

namespace ImageMounter.DevIo.Server.Services
{

    /// <summary>
    /// Class deriving from DevioServiceBase, but without providing a proxy service. Instead,
    /// it just passes a disk image file name or RAM disk information for direct mounting
    /// internally in Arsenal Image Mounter SCSI Adapter.
    /// </summary>
    public class DevioNoneService : DevioServiceBase
    {

        /// <summary>
        /// Name and path of image file mounted by Arsenal Image Mounter.
        /// </summary>
        public string Imagefile { get; }

        /// <summary>
        /// FileAccess flags specifying whether to mount read-only or read-write.
        /// </summary>
        public FileAccess DiskAccess { get; }

        /// <summary>
        /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
        /// Instead, it just passes a disk image file name for direct mounting internally in
        /// SCSI Adapter.
        /// </summary>
        /// <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
        public DevioNoneService(string Imagefile, FileAccess DiskAccess) : base(
            new DummyProvider(new FileInfo(Imagefile).Length), OwnsProvider: true)
        {
            Offset = GetOffsetByFileExt(Imagefile);

            DiskAccess = DiskAccess;

            Imagefile = Imagefile;

            if (!DiskAccess.HasFlag(FileAccess.Write))
                ProxyModeFlags = DeviceFlags.TypeFile | DeviceFlags.ReadOnly;
            else
                ProxyModeFlags = DeviceFlags.TypeFile;

            ProxyObjectName = Imagefile;
        }

        /// <summary>
        /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
        /// Instead, it just passes a disk image file name for direct mounting internally in
        /// SCSI Adapter.
        /// </summary>
        /// <param name="Imagefile">Name and path of image file mounted by Arsenal Image Mounter.</param>
        public DevioNoneService(string Imagefile, DevioServiceFactory.VirtualDiskAccess DiskAccess) : this(Imagefile,
            DevioServiceFactory.GetDirectFileAccessFlags(DiskAccess))
        {
        }

        /// <summary>
        /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
        /// Instead, it just passes a disk size for directly mounting a RAM disk internally in
        /// SCSI Adapter.
        /// </summary>
        /// <param name="DiskSize">Size in bytes of RAM disk to create.</param>
        public DevioNoneService(long DiskSize) : base(new DummyProvider(DiskSize), OwnsProvider: true)
        {
            DiskAccess = FileAccess.ReadWrite;

            if (NativeFileIO.TestFileOpen(@"\\?\awealloc"))
                AdditionalFlags = DeviceFlags.TypeFile | DeviceFlags.FileTypeAwe;
            else
                AdditionalFlags = DeviceFlags.TypeVM;
        }

        private static long GetVhdSize(string Imagefile)
        {
            using (var disk = VirtualDisk.OpenDisk(Imagefile, FileAccess.Read))
            {
                return disk.Capacity;
            }
        }

        /// <summary>
        /// Creates a DevioServiceBase compatible object, but without providing a proxy service.
        /// Instead, it just requests the SCSI adapter, awealloc and vhdaccess drivers to create
        /// a dynamically expanding RAM disk based on the contents of the supplied VHD image.
        /// </summary>
        /// <param name="Imagefile">Path to VHD image file to use as template for the RAM disk.</param>
        public DevioNoneService(string Imagefile) : base(new DummyProvider(GetVhdSize(Imagefile)), OwnsProvider: true)
        {
            DiskAccess = FileAccess.ReadWrite;

            Imagefile = Imagefile;

            ProxyObjectName = @"\\?\vhdaccess\??\awealloc" + NativeFileIO.GetNtPath(Imagefile);
        }

        protected override string ProxyObjectName { get; }

        protected override DeviceFlags ProxyModeFlags { get; }

        /// <summary>
        /// Dummy implementation that always returns True.
        /// </summary>
        /// <returns>Fixed value of True.</returns>
        public override bool StartServiceThread()
        {
            RunService();
            return true;
        }

        /// <summary>
        /// Dummy implementation that just raises ServiceReady event.
        /// </summary>
        public override void RunService()
        {
            OnServiceReady(EventArgs.Empty);
        }

        public override void DismountAndStopServiceThread()
        {
            base.DismountAndStopServiceThread();
            OnServiceShutdown(EventArgs.Empty);
        }

        public override bool DismountAndStopServiceThread(TimeSpan timeout)
        {
            var rc = base.DismountAndStopServiceThread(timeout);
            OnServiceShutdown(EventArgs.Empty);
            return rc;
        }

        protected override void EmergencyStopServiceThread()
        {
        }
    }
}
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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

using ImageMounter.IO;
using Microsoft.Win32.SafeHandles;
using ImageMounter.IO.Native;
using ImageMounter.IO.Native.Enum;
using ImageMounter.IO.Native.Struct;

namespace ImageMounter
{


    /// <summary>

    /// ''' Represents disk objects, attached to a virtual or physical SCSI adapter.

    /// ''' </summary>
    public class DiskDevice : DeviceObject
    {
        private DiskStream _RawDiskStream;

        private SCSI_ADDRESS? _CachedAddress;

        /// <summary>
        /// Returns the device path used to open this device object, if opened by name.
        /// If the object was opened in any other way, such as by supplying an already
        /// open handle, this property returns null/Nothing.
        /// </summary>
        public string DevicePath { get; }

        private void AllowExtendedDasdIo()
        {
            if (!UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                    NativeConstants.FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0U, IntPtr.Zero, 0U, 0U, IntPtr.Zero))
            {
                var errcode = Marshal.GetLastWin32Error();
                if (errcode != NativeConstants.ERROR_INVALID_PARAMETER &&
                    errcode != NativeConstants.ERROR_INVALID_FUNCTION)
                    Trace.WriteLine($"FSCTL_ALLOW_EXTENDED_DASD_IO failed for '{DevicePath}': {errcode}");
            }
        }

        protected internal DiskDevice(KeyValuePair<string, SafeFileHandle> DeviceNameAndHandle, FileAccess AccessMode) :
            base(DeviceNameAndHandle.Value, AccessMode)
        {
            DevicePath = DeviceNameAndHandle.Key;

            AllowExtendedDasdIo();
        }

        /// <summary>
        /// Opens an disk device object without requesting read or write permissions. The
        /// resulting object can only be used to query properties like SCSI address, disk
        /// size and similar, but not for reading or writing raw disk data.
        /// </summary>
        /// <param name="DevicePath"></param>
        public DiskDevice(string DevicePath) : base(DevicePath)
        {
            DevicePath = DevicePath;

            AllowExtendedDasdIo();
        }

        /// <summary>
        /// Opens an disk device object, requesting read, write or both permissions.
        /// </summary>
        /// <param name="DevicePath"></param>
        /// <param name="AccessMode"></param>
        public DiskDevice(string DevicePath, FileAccess AccessMode) : base(DevicePath, AccessMode)
        {
            DevicePath = DevicePath;

            AllowExtendedDasdIo();
        }

        /// <summary>
        /// Opens an disk device object.
        /// </summary>
        /// <param name="ScsiAddress"></param>
        /// <param name="AccessMode"></param>
        public DiskDevice(SCSI_ADDRESS ScsiAddress, FileAccess AccessMode) : this(
            NativeFileIO.OpenDiskByScsiAddress(ScsiAddress, AccessMode), AccessMode)
        {
        }

        /// <summary>
        /// Retrieves device number for this disk on the owner SCSI adapter.
        /// </summary>
        public UInt32 DeviceNumber
        {
            get
            {
                if (_CachedAddress == null)
                {
                    var scsi_address = ScsiAddress.Value;

                    using (ScsiAdapter driver = new ScsiAdapter(scsi_address.PortNumber))
                    {
                    }

                    _CachedAddress = scsi_address;
                }

                return _CachedAddress.Value.DWordDeviceNumber;
            }
        }

        /// <summary>
        /// Retrieves SCSI address for this disk.
        /// </summary>
        public SCSI_ADDRESS? ScsiAddress
        {
            get { return NativeFileIO.GetScsiAddress(SafeFileHandle); }
        }

        /// <summary>
        /// Retrieves storage device type and physical disk number information.
        /// </summary>
        public STORAGE_DEVICE_NUMBER? StorageDeviceNumber
        {
            get { return NativeFileIO.GetStorageDeviceNumber(SafeFileHandle); }
        }

        /// <summary>
        /// Retrieves StorageStandardProperties information.
        /// </summary>
        public StorageStandardProperties? StorageStandardProperties
        {
            get { return NativeFileIO.GetStorageStandardProperties(SafeFileHandle); }
        }

        /// <summary>
        /// Retrieves TRIM enabled information.
        /// </summary>
        public bool? TrimEnabled
        {
            get { return NativeFileIO.GetStorageTrimProperties(SafeFileHandle); }
        }

        /// <summary>
        /// Enumerates disk volumes that use extents of this disk.
        /// </summary>
        public IEnumerable<string> EnumerateDiskVolumes()
        {
            var disk_number = StorageDeviceNumber;

            if (!disk_number.HasValue)
                return null;

            Trace.WriteLine($"Found disk number: {disk_number.Value.DeviceNumber}");

            return NativeFileIO.EnumerateDiskVolumes(disk_number.Value.DeviceNumber);
        }

        /// <summary>
        /// Opens SCSI adapter that created this virtual disk.
        /// </summary>
        public ScsiAdapter OpenAdapter()
        {
            return new ScsiAdapter(NativeFileIO.GetScsiAddress(SafeFileHandle).Value.PortNumber);
        }

        /// <summary>
        /// Updates disk properties by re-enumerating partition table.
        /// </summary>
        public void UpdateProperties()
        {
            NativeFileIO.UpdateDiskProperties(SafeFileHandle, throwOnFailure: true);
        }

        /// <summary>
        /// Retrieves the physical location of a specified volume on one or more disks. 
        /// </summary>
        /// <returns></returns>
        public DiskExtent[] GetVolumeDiskExtents()
        {
            return NativeFileIO.GetVolumeDiskExtents(SafeFileHandle);
        }

        /// <summary>
        /// Gets or sets disk signature stored in boot record.
        /// </summary>
        public UInt32? DiskSignature
        {
            get
            {
                var rawsig = new byte[Geometry.Value.BytesPerSector];

                {
                    var withBlock = GetRawDiskStream();
                    withBlock.Position = 0;
                    withBlock.Read(rawsig, 0, rawsig.Length);
                }
                if (BitConverter.ToUInt16(rawsig, 0x1FE) == 0xAA55 && rawsig[0x1C2] != 0xEE &&
                    (rawsig[0x1BE] & 0x7F) == 0 && (rawsig[0x1CE] & 0x7F) == 0 && (rawsig[0x1DE] & 0x7F) == 0 &&
                    (rawsig[0x1EE] & 0x7F) == 0)
                    return BitConverter.ToUInt32(rawsig, 0x1B8);

                return null;
            }
            set
            {
                if (!value.HasValue)
                    return;
                var newvalue = BitConverter.GetBytes(value.Value);

                var rawsig = new byte[Geometry.Value.BytesPerSector];


                {
                    var withBlock = GetRawDiskStream();
                    withBlock.Position = 0;
                    withBlock.Read(rawsig, 0, rawsig.Length);
                    Buffer.BlockCopy(newvalue, 0, rawsig, 0x1B8, newvalue.Length);
                    withBlock.Position = 0;
                    withBlock.Write(rawsig, 0, rawsig.Length);
                }
            }
        }

        /// <summary>
        /// Gets or sets disk signature stored in boot record.
        /// </summary>
        public UInt32? VBRHiddenSectorsCount
        {
            get
            {

                var rawsig = new byte[Geometry.Value.BytesPerSector];

                {
                    var withBlock = GetRawDiskStream();
                    withBlock.Position = 0;
                    withBlock.Read(rawsig, 0, rawsig.Length);
                }

                if (BitConverter.ToUInt16(rawsig, 0x1FE) == 0xAA55)
                    return BitConverter.ToUInt32(rawsig, 0x1);

                return null;
            }
            set
            {
                if (!value.HasValue)
                    return;
                var newvalue = BitConverter.GetBytes(value.Value);

                var rawsig = new byte[Geometry.Value.BytesPerSector];


                {
                    var withBlock = GetRawDiskStream();
                    withBlock.Position = 0;
                    withBlock.Read(rawsig, 0, rawsig.Length);
                    Buffer.BlockCopy(newvalue, 0, rawsig, 0x1, newvalue.Length);
                    withBlock.Position = 0;
                    withBlock.Write(rawsig, 0, rawsig.Length);
                }
            }
        }

        /// <summary>
        /// Reads first sector of disk or disk volume
        /// </summary>
        public byte[] ReadBootSector()
        {
            var bootsect = new byte[Geometry.Value.BytesPerSector];


            int bytesread;

            {
                var withBlock = GetRawDiskStream();
                withBlock.Position = 0;
                bytesread = withBlock.Read(bootsect, 0, bootsect.Length);
            }

            if (bytesread < 512)
                return null;

            if (bytesread != bootsect.Length)
                Array.Resize(ref bootsect, bytesread);

            return bootsect;
        }

        /// <summary>
        /// Return a value indicating whether present sector 0 data indicates a valid MBR
        /// with a partition table.
        /// </summary>
        public bool HasValidPartitionTable
        {
            get
            {
                var bootsect = ReadBootSector();

                return BitConverter.ToUInt16(bootsect, 0x1FE) == 0xAA55 && (bootsect[0x1BE] & 0x7F) == 0 &&
                       (bootsect[0x1CE] & 0x7F) == 0 && (bootsect[0x1DE] & 0x7F) == 0 && (bootsect[0x1EE] & 0x7F) == 0;
            }
        }

        /// <summary>
        /// Return a value indicating whether present sector 0 data indicates a valid MBR
        /// with a partition table and not blank or fake boot code.
        /// </summary>
        public bool HasValidBootCode
        {
            get
            {
                var bootsect = ReadBootSector();

                if (bootsect == null || bootsect[0] == 0 || bootsect.AsSpan(0, NativeConstants.DefaultBootCode.Length)
                        .SequenceEqual(NativeConstants.DefaultBootCode.Span))
                    return false;

                return BitConverter.ToUInt16(bootsect, 0x1FE) == 0xAA55 && (bootsect[0x1BE] & 0x7F) == 0 &&
                       (bootsect[0x1CE] & 0x7F) == 0 && (bootsect[0x1DE] & 0x7F) == 0 && (bootsect[0x1EE] & 0x7F) == 0;
            }
        }

        /// <summary>
        /// Flush buffers for a disk or volume.
        /// </summary>
        public void FlushBuffers()
        {
            if (_RawDiskStream != null)
                _RawDiskStream.Flush();
            else
                NativeFileIO.FlushBuffers(SafeFileHandle);
        }

        /// <summary>
        /// Gets or sets physical disk offline attribute. Only valid for
        /// physical disk objects, not volumes or partitions.
        /// </summary>
        public bool? DiskPolicyOffline
        {
            get { return NativeFileIO.GetDiskOffline(SafeFileHandle); }
            set
            {
                if (Value.HasValue)
                    NativeFileIO.SetDiskOffline(SafeFileHandle, Value.Value);
            }
        }

        /// <summary>
        /// Gets or sets physical disk read only attribute. Only valid for
        /// physical disk objects, not volumes or partitions.
        /// </summary>
        public bool? DiskPolicyReadOnly
        {
            get { return NativeFileIO.GetDiskReadOnly(SafeFileHandle); }
            set
            {
                if (Value.HasValue)
                    NativeFileIO.SetDiskReadOnly(SafeFileHandle, Value.Value);
            }
        }

        /// <summary>
        /// Sets disk volume offline attribute. Only valid for logical
        /// disk volumes, not physical disk drives.
        /// </summary>
        public void SetVolumeOffline(bool value)
        {
            NativeFileIO.SetVolumeOffline(SafeFileHandle, value);
        }

        /// <summary>
        /// Gets information about a partition stored on a disk with MBR
        /// partition layout. This property is not available for physical
        /// disks, only disk partitions are supported.
        /// </summary>
        public PARTITION_INFORMATION? PartitionInformation
        {
            get { return NativeFileIO.GetPartitionInformation(SafeFileHandle); }
        }

        /// <summary>
        /// Gets information about a disk partition. This property is not
        /// available for physical disks, only disk partitions are supported.
        /// </summary>
        public PARTITION_INFORMATION_EX? PartitionInformationEx
        {
            get { return NativeFileIO.GetPartitionInformationEx(SafeFileHandle); }
        }

        /// <summary>
        /// Gets information about a disk partitions. This property is available
        /// for physical disks, not disk partitions.
        /// </summary>
        public NativeFileIO.DriveLayoutInformation DriveLayoutEx
        {
            get { return NativeFileIO.GetDriveLayoutEx(SafeFileHandle); }
            set { NativeFileIO.SetDriveLayoutEx(SafeFileHandle, Value); }
        }

        /// <summary>
        /// Initialize a raw disk device for use with Windows. This method is available
        /// for physical disks, not disk partitions.
        /// </summary>
        public void InitializeDisk(PARTITION_STYLE PartitionStyle)
        {
            NativeFileIO.InitializeDisk(SafeFileHandle, PartitionStyle);
        }

        /// <summary>
        /// Disk identifier string.
        /// </summary>
        /// <returns>8 digit hex string for MBR disks or disk GUID for
        /// GPT disks.</returns>
        public string DiskId
        {
            get { return DriveLayoutEx?.ToString() ?? "(Unknown)"; }
        }

        /// <summary>
        /// Retrieves properties for an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk.</param>
        /// <param name="DiskSize">Size of virtual disk.</param>
        /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
        /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter or Windows
        /// filesystem drivers.</param>
        /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
        /// <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
        /// virtual memory type virtual disk.</param>
        public void QueryDevice(out UInt32 DeviceNumber, out Int64 DiskSize, out UInt32 BytesPerSector,
            out Int64 ImageOffset, out DeviceFlags Flags, out string Filename)
        {
            var scsi_address = ScsiAddress.Value;

            using (ScsiAdapter adapter = new ScsiAdapter(scsi_address.PortNumber))
            {
                DeviceNumber = scsi_address.DWordDeviceNumber;

                adapter.QueryDevice(DeviceNumber, DiskSize, BytesPerSector, ImageOffset, Flags, Filename);
            }
        }

        /// <summary>
        /// Retrieves properties for an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk.</param>
        /// <param name="DiskSize">Size of virtual disk.</param>
        /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
        /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter or Windows
        /// filesystem drivers.</param>
        /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
        /// <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
        /// virtual memory type virtual disk.</param>
        /// <param name="WriteOverlayImagefile">Path to differencing file used in write-temporary mode.</param>
        public void QueryDevice(out UInt32 DeviceNumber, out Int64 DiskSize, out UInt32 BytesPerSector,
            out Int64 ImageOffset, out DeviceFlags Flags, out string Filename, out string WriteOverlayImagefile)
        {
            var scsi_address = ScsiAddress.Value;

            using (ScsiAdapter adapter = new ScsiAdapter(scsi_address.PortNumber))
            {
                DeviceNumber = scsi_address.DWordDeviceNumber;

                adapter.QueryDevice(DeviceNumber, DiskSize, BytesPerSector, ImageOffset, Flags, Filename,
                    WriteOverlayImagefile);
            }
        }

        /// <summary>
        /// Retrieves properties for an existing virtual disk.
        /// </summary>
        public DeviceProperties QueryDevice()
        {
            var scsi_address = ScsiAddress.Value;

            using (ScsiAdapter adapter = new ScsiAdapter(scsi_address.PortNumber))
            {
                return adapter.QueryDevice(scsi_address.DWordDeviceNumber);
            }
        }

        /// <summary>
        /// Removes this virtual disk from adapter.
        /// </summary>
        public void RemoveDevice()
        {
            var scsi_address = ScsiAddress.Value;

            using (ScsiAdapter adapter = new ScsiAdapter(scsi_address.PortNumber))
            {
                adapter.RemoveDevice(scsi_address.DWordDeviceNumber);
            }
        }

        /// <summary>
        /// Retrieves volume size of disk device.
        /// </summary>
        public long? DiskSize
        {
            get { return NativeFileIO.GetDiskSize(SafeFileHandle); }
        }

        /// <summary>
        /// Retrieves partition information.
        /// </summary>
        /// <returns></returns>
        public FILE_FS_FULL_SIZE_INFORMATION? VolumeSizeInformation
        {
            get { return NativeFileIO.GetVolumeSizeInformation(SafeFileHandle); }
        }

        /// <summary>
        /// Determines whether disk is writable or read-only.
        /// </summary>
        public bool IsDiskWritable
        {
            get { return NativeFileIO.IsDiskWritable(SafeFileHandle); }
        }

        /// <summary>
        /// Returns logical disk geometry. Normally, only the BytesPerSector member
        /// contains data of interest.
        /// </summary>
        public DISK_GEOMETRY? Geometry => NativeFileIO.GetDiskGeometry(SafeFileHandle);

        /// <summary>
        /// Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
        /// can only be done through this device object instance until it is either closed (disposed) or lock is
        /// released on the underlying handle.
        /// </summary>
        /// <param name="Force">Indicates if True that volume should be immediately dismounted even if it
        /// cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
        /// successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
        public void DismountVolumeFilesystem(bool Force)
        {
            NativeFileIO.Win32Try(NativeFileIO.DismountVolumeFilesystem(SafeFileHandle, Force));
        }


        /// <summary>
        /// Get live statistics from write filter driver.
        /// </summary>
        public WriteFilterStatistics? WriteOverlayStatus
        {
            get
            {
                WriteFilterStatistics statistics = null /* TODO Change to default(_) if this is not a reference type */;

                if (API.GetWriteOverlayStatus(SafeFileHandle, statistics) != NativeConstants.NO_ERROR)
                    return default(WriteFilterStatistics?);

                return statistics;
            }
        }

        /// <summary>
        /// Deletes the write overlay image file after use. Also sets the filter driver to
        /// silently ignore flush requests to improve performance when integrity of the write
        /// overlay image is not needed for future sessions.
        /// </summary>
        public void SetWriteOverlayDeleteOnClose()
        {
            var rc = API.SetWriteOverlayDeleteOnClose(SafeFileHandle);
            if (rc != NativeConstants.NO_ERROR)
                throw new Win32Exception(rc);
        }

        /// <summary>
        /// Returns an DiskStream object that can be used to directly access disk data.
        /// The returned stream automatically sector-aligns I/O.
        /// </summary>
        public DiskStream GetRawDiskStream()
        {
            if (_RawDiskStream == null)
                _RawDiskStream = new DiskStream(SafeFileHandle, AccessMode == 0 ? FileAccess.Read : AccessMode);

            return _RawDiskStream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _RawDiskStream?.Dispose();

            _RawDiskStream = null;

            base.Dispose(disposing);
        }
    }
}
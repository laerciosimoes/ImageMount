using System;
using System.Collections.Generic;

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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using ImageMounter.Interop.IO;
using ImageMounter.IO;
using Microsoft.Win32.SafeHandles;
using ImageMounter.IO.Native;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter
{

    /// <summary>
    /// Represents Arsenal Image Mounter objects.
    /// </summary>
    public class ScsiAdapter : DeviceObject
    {
        public const uint CompatibleDriverVersion = 0x101;

        public const UInt32 AutoDeviceNumber = 0xFFFFFF;

        public string DeviceInstanceName { get; }

        public uint DeviceInstance { get; }

        private static SafeFileHandle OpenAdapterHandle(string ntdevice, uint devInst)
        {
            SafeFileHandle handle;
            try
            {
                handle = NativeFileIO.NtCreateFile(ntdevice, 0, FileAccess.ReadWrite, FileShare.ReadWrite,
                    NtCreateDisposition.Open, 0, 0,
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"PhDskMnt::OpenAdapterHandle: Error opening device '{ntdevice}': {ex.JoinMessages()}");

                return null;
            }

            bool acceptedversion;
            for (var i = 1; i <= 3; i++)
            {
                try
                {
                    acceptedversion = CheckDriverVersion(handle);
                    if (acceptedversion)
                        return handle;
                    else
                    {
                        handle.Dispose();
                        throw new Exception("Incompatible version of Arsenal Image Mounter Miniport driver.");
                    }
                }
                catch (Win32Exception ex) when ((ex.NativeErrorCode == NativeConstants.ERROR_INVALID_FUNCTION) ||
                                                (ex.NativeErrorCode == NativeConstants.ERROR_IO_DEVICE))
                {

                    // ' In case of SCSIPORT (Win XP) miniport, there is always a risk
                    // ' that we lose contact with IOCTL_SCSI_MINIPORT after device adds
                    // ' and removes. Therefore, in case we know that we have a handle to
                    // ' the SCSI adapter and it fails IOCTL_SCSI_MINIPORT requests, just
                    // ' issue a bus re-enumeration to find the dummy IOCTL device, which
                    // ' will make SCSIPORT let control requests through again.
                    if (!API.HasStorPort)
                    {
                        Trace.WriteLine("PhDskMnt::OpenAdapterHandle: Lost contact with miniport, rescanning...");
                        try
                        {
                            API.RescanScsiAdapter(devInst);
                            Thread.Sleep(100);
                            continue;
                        }
                        catch (Exception ex2)
                        {
                            Trace.WriteLine($"PhDskMnt::RescanScsiAdapter: {ex2}");
                        }
                    }

                    handle.Dispose();
                    return null;
                }

                catch (Exception ex)
                {
                    if (ex is Win32Exception)
                        Trace.WriteLine($"Error code 0x{(Win32Exception)ex.NativeErrorCode}");
                    Trace.WriteLine($"PhDskMnt::OpenAdapterHandle: Error checking driver version: {ex.JoinMessages()}");
                    handle.Dispose();
                    return null;
                }
            }

            return null;
        }

        private sealed class AdapterDeviceInstance
        {
            public string DevInstName { get; }

            public uint DevInst { get; }

            public SafeFileHandle SafeHandle { get; }

            public AdapterDeviceInstance(string devInstName, uint devInst, SafeFileHandle safeHhandle)
            {
                DevInstName = devInstName;
                DevInst = devInst;
                SafeHandle = safeHhandle;
            }
        }

        /// <summary>
        /// Retrieves a handle to first found adapter, or null if error occurs.
        /// </summary>
        /// <remarks>Arsenal Image Mounter does not currently support more than one adapter.</remarks>
        /// <returns>An object containing devinst value and an open handle to first found
        /// compatible adapter.</returns>
        private static AdapterDeviceInstance OpenAdapter()
        {
            var devinstNames = API.EnumerateAdapterDeviceInstanceNames();

            if (devinstNames == null)
                throw new FileNotFoundException("No Arsenal Image Mounter adapter found.");

            var found = devinstNames.FirstOrDefault();
            /*
        Dim found = Aggregate devInstName In devinstNames
                    Let devinst = NativeFileIO.GetDevInst(devInstName)
                    Where devinst.HasValue
                    Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinst.Value)
                    Where path IsNot Nothing
                    Let handle = OpenAdapterHandle(path, devinst.Value)
                    Where handle IsNot Nothing
                    Select New AdapterDeviceInstance(devInstName, devinst.Value, handle)
                    Into FirstOrDefault();
            */
            if (found == null)
                throw new FileNotFoundException("No Arsenal Image Mounter adapter found.");

            return found;
        }

        /// <summary>
        /// Opens first found Arsenal Image Mounter adapter.
        /// </summary>
        public ScsiAdapter() : this(OpenAdapter())
        {
        }

        private ScsiAdapter(AdapterDeviceInstance OpenAdapterHandle) : base(OpenAdapterHandle.SafeHandle, FileAccess.ReadWrite)
        {
            DeviceInstance = OpenAdapterHandle.DevInst;
            DeviceInstanceName = OpenAdapterHandle.DevInstName;

            Trace.WriteLine($"Successfully opened SCSI adapter '{OpenAdapterHandle.DevInstName}'.");
        }

        /// <summary>
        /// Opens a specific Arsenal Image Mounter adapter specified by SCSI port number.
        /// </summary>
        /// <param name="ScsiPortNumber">Scsi adapter port number as assigned by SCSI class driver.</param>
        public ScsiAdapter(byte ScsiPortNumber) : base($@"\\?\Scsi{ScsiPortNumber}:", FileAccess.ReadWrite)
        {
            Trace.WriteLine($"Successfully opened adapter with SCSI portnumber = {ScsiPortNumber}.");

            if (!CheckDriverVersion())
                throw new Exception("Incompatible version of Arsenal Image Mounter Miniport driver.");
        }

        /// <summary>
        /// Retrieves a list of virtual disks on this adapter. Each element in returned list holds device number of an existing virtual disk.
        /// </summary>
        public uint[] GetDeviceList()
        {
            Int32 ReturnCode;

            var Response =
                NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                    NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_ADAPTER,
                    0,
                    new Byte[65535],
                    ReturnCode);

            if (ReturnCode != 0)
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);

            var NumberOfDevices = BitConverter.ToInt32(Response, 0);

        var  array = new uint[NumberOfDevices - 1];
            Buffer.BlockCopy(Response, 4, array, 0, NumberOfDevices * 4);

            return array;
        }

        /// <summary>
        /// Retrieves a list of DeviceProperties objects for each virtual disk on this adapter.
        /// </summary>
        public IEnumerable<DeviceProperties> EnumerateDevicesProperties()
        {
            return GetDeviceList().Select(QueryDevice);
        }

        /// <summary>
        /// Creates a new virtual disk.
        /// </summary>
        /// <param name="DiskSize">Size of virtual disk. If this parameter is zero, current size of disk image file will
        /// automatically be used as virtual disk size.</param>
        /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry. This parameter can be zero
        /// in which case most reasonable value will be automatically used by the driver.</param>
        /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
        /// or Windows filesystem drivers.</param>
        /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
        /// <param name="Filename">Name of disk image file to use or create. If disk image file already exists, the DiskSize
        /// parameter can be zero in which case current disk image file size will be used as virtual disk size. If Filename
        /// parameter is Nothing/null disk will be created in virtual memory and not backed by a physical disk image file.</param>
        /// <param name="NativePath">Specifies whether Filename parameter specifies a path in Windows native path format, the
        /// path format used by drivers in Windows NT kernels, for example \Device\Harddisk0\Partition1\imagefile.img. If this
        /// parameter is False path in FIlename parameter will be interpreted as an ordinary user application path.</param>
        /// <param name="DeviceNumber">In: Device number for device to create. Device number must not be in use by an existing
        /// virtual disk. For automatic allocation of device number, pass ScsiAdapter.AutoDeviceNumber.
        /// 
        /// Out: Device number for created device.</param>
        public void CreateDevice(Int64 DiskSize, UInt32 BytesPerSector, Int64 ImageOffset, DeviceFlags Flags,
            string Filename, bool NativePath, ref UInt32 DeviceNumber)
        {
            CreateDevice(DiskSize, BytesPerSector, ImageOffset, Flags, Filename, NativePath,
                WriteOverlayFilename: null /* TODO Change to default(_) if this is not a reference type */,
                WriteOverlayNativePath: null /* TODO Change to default(_) if this is not a reference type */,
                DeviceNumber: ref DeviceNumber);
        }

        /// <summary>
        /// Creates a new virtual disk.
        /// </summary>
        /// <param name="DiskSize">Size of virtual disk. If this parameter is zero, current size of disk image file will
        /// automatically be used as virtual disk size.</param>
        /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry. This parameter can be zero
        ///  in which case most reasonable value will be automatically used by the driver.</param>
        /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
        /// or Windows filesystem drivers.</param>
        /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
        /// <param name="Filename">Name of disk image file to use or create. If disk image file already exists, the DiskSize
        /// parameter can be zero in which case current disk image file size will be used as virtual disk size. If Filename
        /// parameter is Nothing/null disk will be created in virtual memory and not backed by a physical disk image file.</param>
        /// <param name="NativePath">Specifies whether Filename parameter specifies a path in Windows native path format, the
        /// path format used by drivers in Windows NT kernels, for example \Device\Harddisk0\Partition1\imagefile.img. If this
        /// parameter is False path in Filename parameter will be interpreted as an ordinary user application path.</param>
        /// <param name="WriteOverlayFilename">Name of differencing image file to use for write overlay operation. Flags fields
        /// must also specify read-only device and write overlay operation for this field to be used.</param>
        /// <param name="WriteOverlayNativePath">Specifies whether WriteOverlayFilename parameter specifies a path in Windows
        /// native path format, the path format used by drivers in Windows NT kernels, for example
        /// \Device\Harddisk0\Partition1\imagefile.img. If this parameter is False path in Filename parameter will be interpreted
        /// as an ordinary user application path.</param>
        /// <param name="DeviceNumber">In: Device number for device to create. Device number must not be in use by an existing
        /// virtual disk. For automatic allocation of device number, pass ScsiAdapter.AutoDeviceNumber.
        ///     '''
        /// Out: Device number for created device.</param>
        public void CreateDevice(Int64 DiskSize, UInt32 BytesPerSector, Int64 ImageOffset, DeviceFlags Flags,
            string Filename, bool NativePath, string WriteOverlayFilename, bool WriteOverlayNativePath,
            ref UInt32 DeviceNumber)
        {

            // ' Temporary variable for passing through lambda function
            var devnr = DeviceNumber;

            // ' Both UInt32.MaxValue and AutoDeviceNumber can be used
            // ' for auto-selecting device number, but only AutoDeviceNumber
            // ' is accepted by driver.
            if (devnr == uint.MaxValue)
                devnr = AutoDeviceNumber;

            // ' Translate Win32 path to native NT path that kernel understands
            if ((!string.IsNullOrEmpty(Filename)) && (!NativePath))
            {
                switch (Flags.GetDiskType())
                {
                    case object _ when DeviceFlags.TypeProxy:
                    {
                        switch (Flags.GetProxyType())
                        {
                            case object _ when DeviceFlags.ProxyTypeSharedMemory:
                            {
                                Filename = $@"\BaseNamedObjects\Global\{Filename}";
                                break;
                            }

                            case object _ when DeviceFlags.ProxyTypeComm:
                            case object _ when DeviceFlags.ProxyTypeTCP:
                            {
                                break;
                            }

                            default:
                            {
                                Filename = NativeFileIO.GetNtPath(Filename);
                                break;
                            }
                        }

                        break;
                    }

                    default:
                    {
                        Filename = NativeFileIO.GetNtPath(Filename);
                        break;
                    }
                }
            }

            // ' Show what we got
            Trace.WriteLine($"ScsiAdapter.CreateDevice: Native filename='{Filename}'");

            GlobalCriticalMutex
                write_filter_added = null /* TODO Change to default(_) if this is not a reference type */;

            try
            {
                if (!string.IsNullOrWhiteSpace(WriteOverlayFilename))
                {
                    if ((!WriteOverlayNativePath))
                        WriteOverlayFilename = NativeFileIO.GetNtPath(WriteOverlayFilename);

                    Trace.WriteLine(
                        $"ScsiAdapter.CreateDevice: Thread {Thread.CurrentThread.ManagedThreadId} entering global critical section");

                    write_filter_added = new GlobalCriticalMutex();

                    NativeFileIO.AddFilter(NativeConstants.DiskDriveGuid, "aimwrfltr", addfirst: true);
                }

                // ' Show what we got
                Trace.WriteLine($"ScsiAdapter.CreateDevice: Native write overlay filename='{WriteOverlayFilename}'");
                

            var ReservedField = byte[3];
                
                if (!string.IsNullOrWhiteSpace(WriteOverlayFilename))
                {
                    var bytes = BitConverter.GetBytes(System.Convert.ToUInt16(WriteOverlayFilename.Length * 2));
                    Buffer.BlockCopy(bytes, 0, ReservedField, 0, bytes.Length);
                }

                BufferedBinaryWriter Request = new BufferedBinaryWriter();
                Request.Write(devnr);
                Request.Write(DiskSize);
                Request.Write(BytesPerSector);
                Request.Write(ReservedField);
                Request.Write(ImageOffset);
                Request.Write(System.Convert.ToUInt32(Flags));
                if (string.IsNullOrEmpty(Filename))
                    Request.Write(0);
                else
                {
                    var bytes = Encoding.Unicode.GetBytes(Filename);
                    Request.Write(System.Convert.ToUInt16(bytes.Length));
                    Request.Write(bytes);
                }

                if (!string.IsNullOrWhiteSpace(WriteOverlayFilename))
                {
                    var bytes = Encoding.Unicode.GetBytes(WriteOverlayFilename);
                    Request.Write(bytes);
                }

                Int32 ReturnCode;

                var outbuffer = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                    NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_CREATE_DEVICE, 0, Request.ToArray(), ReturnCode);

                if (ReturnCode != 0)
                    throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);

                using (BinaryReader Response = new BinaryReader(new MemoryStream(outbuffer)))
                {
                    DeviceNumber = Response.ReadUInt32();
                    DiskSize = Response.ReadInt64();
                    BytesPerSector = Response.ReadUInt32();
                    ReservedField = Response.ReadBytes(4);
                    ImageOffset = Response.ReadInt64();
                    Flags = (DeviceFlags)Response.ReadUInt32();
                }

                while (!GetDeviceList().Contains(DeviceNumber))
                {
                    Trace.WriteLine($"Waiting for new device {DeviceNumber} to be registered by driver...");
                    Thread.Sleep(2500);
                }

                DiskDevice DiskDevice;

                var waittime = TimeSpan.FromMilliseconds(500);
                do
                {
                    Thread.Sleep(waittime);
                    try
                    {
                        DiskDevice = OpenDevice(DeviceNumber, FileAccess.Read);
                    }
                    catch (DriveNotFoundException ex)
                    {
                        Trace.WriteLine($"Error opening device: {ex.JoinMessages()}");
                        waittime += TimeSpan.FromMilliseconds(500);
                        Trace.WriteLine("Not ready, rescanning SCSI adapter...");
                        RescanBus();
                        continue;
                    }

                    using (DiskDevice)
                    {
                        if (DiskDevice.DiskSize == 0)
                        {

                            // ' Wait at most 20 x 500 msec for device to get initialized by driver
                            for (var i = 1; i <= 20; i++)
                            {
                                Thread.Sleep(500 * i);
                                if (DiskDevice.DiskSize != 0)
                                    break;
                                Trace.WriteLine("Updating disk properties...");
                                DiskDevice.UpdateProperties();
                            }
                        }

                        if (Flags.HasFlag(DeviceFlags.WriteOverlay) && !string.IsNullOrWhiteSpace(WriteOverlayFilename))
                        {
                            var status = DiskDevice.WriteOverlayStatus;
                            if (status.HasValue)
                            {
                                Trace.WriteLine(
                                    $"Write filter attached, {status.Value.UsedDiffSize} differencing bytes used.");
                                break;
                            }

                            Trace.WriteLine("Write filter not registered. Registering and restarting device...");
                        }
                        else
                            break;
                    }

                    try
                    {
                        API.RegisterWriteFilter(DeviceInstance, DeviceNumber,
                            API.RegisterWriteFilterOperation.Register);
                    }
                    catch (Exception ex)
                    {
                        RemoveDevice(DeviceNumber);
                        throw new Exception("Failed to register write filter driver", ex);
                    }
                } while (true);
            }
            finally
            {
                if (write_filter_added != null)
                {
                    NativeFileIO.RemoveFilter(NativeConstants.DiskDriveGuid, "aimwrfltr");

                    Trace.WriteLine(
                        $"ScsiAdapter.CreateDevice: Thread {Thread.CurrentThread.ManagedThreadId} leaving global critical section");

                    write_filter_added.Dispose();
                }
            }

            Trace.WriteLine("CreateDevice done.");
        }


        /// <summary>
        /// Removes an existing virtual disk from adapter by first taking the disk offline so that any
        /// mounted file systems are safely dismounted.
        /// </summary>
        /// <param name="DeviceNumber">Device number to remove. Note that AutoDeviceNumber constant passed
        /// in this parameter causes all present virtual disks to be removed from this adapter.</param>
        public void RemoveDeviceSafe(UInt32 DeviceNumber)
        {
            if (DeviceNumber == AutoDeviceNumber)
            {
                RemoveAllDevicesSafe();

                return;
            }

            IEnumerable<string> volumes = null;

            using (var disk = OpenDevice(DeviceNumber, FileAccess.ReadWrite))
            {
                if (disk.IsDiskWritable)
                    volumes = disk.EnumerateDiskVolumes();
            }

            if (volumes != null)
            {
                foreach (var volname in volumes.Select(v => v.TrimEnd('\\')))
                {
                    Trace.WriteLine($"Dismounting volume: {volname}");

                    using (var vol = NativeFileIO.OpenFileHandle(volname, FileAccess.ReadWrite, FileShare.ReadWrite,
                               FileMode.Open, FileOptions.None))
                    {
                        if (NativeFileIO.IsDiskWritable(vol))
                        {
                            try
                            {
                                NativeFileIO.FlushBuffers(vol);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine($"Failed flushing buffers for volume {volname}: {ex.JoinMessages()}");
                            }

                            // NativeFileIO.Win32Try(NativeFileIO.DismountVolumeFilesystem(vol, Force:=False))

                            NativeFileIO.SetVolumeOffline(vol, offline: true);
                        }
                    }
                }
            }

            RemoveDevice(DeviceNumber);
        }

        /// <summary>
        /// Removes all virtual disks on current adapter by first taking the disks offline so that any
        /// mounted file systems are safely dismounted.
        /// </summary>
        public void RemoveAllDevicesSafe()
        {
            Parallel.ForEach(GetDeviceList(), RemoveDeviceSafe);
        }

        /// <summary>
        /// Removes an existing virtual disk from adapter.
        /// </summary>
        /// <param name="DeviceNumber">Device number to remove. Note that AutoDeviceNumber constant passed
        /// in this parameter causes all present virtual disks to be removed from this adapter.</param>
        public void RemoveDevice(UInt32 DeviceNumber)
        {
            Int32 ReturnCode;

            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_REMOVE_DEVICE, 0, BitConverter.GetBytes(DeviceNumber), ReturnCode);

            if (ReturnCode == NativeConstants.STATUS_OBJECT_NAME_NOT_FOUND)
                return;
            else if (ReturnCode != 0)
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
        }

        /// <summary>
        /// Removes all virtual disks on current adapter.
        /// </summary>
        public void RemoveAllDevices()
        {
            RemoveDevice(AutoDeviceNumber);
        }

        /// <summary>
        /// Retrieves properties for an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
        /// <param name="DiskSize">Size of virtual disk.</param>
        /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
        /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
        /// or Windows filesystem drivers.</param>
        /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
        /// <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
        /// virtual memory type virtual disk.</param>
        public void QueryDevice(UInt32 DeviceNumber, out Int64 DiskSize, out UInt32 BytesPerSector, out Int64 ImageOffset, out DeviceFlags Flags, out string Filename)
        {
            QueryDevice(DeviceNumber, ref DiskSize, ref BytesPerSector, ref ImageOffset, ref Flags, ref Filename,
                WriteOverlayImagefile: ref null /* TODO Change to default(_) if this is not a reference type */);
        }

        /// <summary>
        /// Retrieves properties for an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
        /// <param name="DiskSize">Size of virtual disk.</param>
        /// <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
        /// <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
        /// or Windows filesystem drivers.</param>
        /// <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
        /// <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
        /// virtual memory type virtual disk.</param>
        /// <param name="WriteOverlayImagefile">Path to differencing file used in write-temporary mode.</param>
        public void QueryDevice(UInt32 DeviceNumber, out Int64 DiskSize, out UInt32 BytesPerSector,
            out Int64 ImageOffset, out DeviceFlags Flags, out string Filename, out string WriteOverlayImagefile)
        {
            BufferedBinaryWriter Request = new BufferedBinaryWriter();
            Request.Write(DeviceNumber);
            Request.Write(0L);
            Request.Write(0U);
            Request.Write(0L);
            Request.Write(0U);
            Request.Write(65535);
            Request.Write(new byte[65534]);

            Int32 ReturnCode;

            var buffer = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_DEVICE, 0, Request.ToArray(), ReturnCode);

            // ' STATUS_OBJECT_NAME_NOT_FOUND. Possible "zombie" device, just return empty data.
            if (ReturnCode == 0xC0000034)
                return;
            else if (ReturnCode != 0)
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);

            using (BinaryReader Response = new BinaryReader(new MemoryStream(buffer)))
            {
                DeviceNumber = Response.ReadUInt32();
                DiskSize = Response.ReadInt64();
                BytesPerSector = Response.ReadUInt32();
                var ReservedField = Response.ReadBytes(4);
                ImageOffset = Response.ReadInt64();
                Flags = (DeviceFlags)Response.ReadUInt32();
                var FilenameLength = Response.ReadUInt16();
                if (FilenameLength == 0)
                    Filename = null;
                else
                    Filename = Encoding.Unicode.GetString(Response.ReadBytes(FilenameLength));
                if (Flags.HasFlag(DeviceFlags.WriteOverlay))
                {
                    var WriteOverlayImagefileLength = BitConverter.ToUInt16(ReservedField, 0);
                    WriteOverlayImagefile = Encoding.Unicode.GetString(Response.ReadBytes(WriteOverlayImagefileLength));
                }
            }
        }

        /// <summary>
        /// Retrieves properties for an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
        public DeviceProperties QueryDevice(UInt32 DeviceNumber)
        {
            return new DeviceProperties(this, DeviceNumber);
        }

        /// <summary>
        /// Modifies properties for an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk to modify properties for.</param>
        /// <param name="FlagsToChange">Flags for which to change values for.</param>
        /// <param name="FlagValues">New flag values.</param>
        public void ChangeFlags(UInt32 DeviceNumber, DeviceFlags FlagsToChange, DeviceFlags FlagValues)
        {
            BufferedBinaryWriter Request = new BufferedBinaryWriter();
            Request.Write(DeviceNumber);
            Request.Write(System.Convert.ToUInt32(FlagsToChange));
            Request.Write(System.Convert.ToUInt32(FlagValues));

            Int32 ReturnCode;

            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_SET_DEVICE_FLAGS, 0, Request.ToArray(), ReturnCode);

            if (ReturnCode != 0)
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
        }

        /// <summary>
        /// Extends size of an existing virtual disk.
        /// </summary>
        /// <param name="DeviceNumber">Device number of virtual disk to modify.</param>
        /// <param name="ExtendSize">Number of bytes to extend.</param>
        public void ExtendSize(UInt32 DeviceNumber, Int64 ExtendSize)
        {
            BufferedBinaryWriter Request = new BufferedBinaryWriter();
            Request.Write(DeviceNumber);
            Request.Write(ExtendSize);

            Int32 ReturnCode;

            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_EXTEND_DEVICE, 0, Request.ToArray(), ReturnCode);

            if (ReturnCode != 0)
                throw NativeFileIO.GetExceptionForNtStatus(ReturnCode);
        }

        /// <summary>
        /// Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
        /// library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
        /// </summary>
        public bool CheckDriverVersion()
        {
            return CheckDriverVersion(SafeFileHandle);
        }

        /// <summary>
        /// Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
        /// library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
        /// </summary>
        public static bool CheckDriverVersion(SafeFileHandle SafeFileHandle)
        {
            Int32 ReturnCode;
            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION, 0,
                null /* TODO Change to default(_) if this is not a reference type */, ReturnCode);

            if (ReturnCode == CompatibleDriverVersion)
                return true;

            Trace.WriteLine($"Library version: {CompatibleDriverVersion}");
            Trace.WriteLine($"Driver version: {ReturnCode}");

            return false;
        }

        /// <summary>
        /// Retrieves the sub version of the driver. This is not the same as the API compatibility version checked for by
        /// CheckDriverVersion method. The version record returned by this GetDriverSubVersion method can be used to find
        /// out whether the latest version of the driver is loaded, for example to show a dialog box asking user whether to
        /// upgrade the driver. If driver does not support this version query, this method returns Nothing/null.
        /// </summary>
        public Version GetDriverSubVersion()
        {
            Int32 ReturnCode;
       
        var Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                                  NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION,
                                                                  0,
                                                                  bew byte[3],
                                                                  ReturnCode);

 
            Trace.WriteLine($"Library version: {CompatibleDriverVersion}");
            Trace.WriteLine($"Driver version: {ReturnCode}");

            if (ReturnCode != CompatibleDriverVersion)
                return null;

            try
            {
                var build = Response(0);
                var low = Response(1);
                var minor = Response(2);
                var major = Response(3);

                return new Version(major, minor, low, build);
            }
            catch (IOException ex)
            {
                return null;
            }
        }

        public bool RescanScsiAdapter()
        {
            return API.RescanScsiAdapter(DeviceInstance);
        }

        /// <summary>
        /// Issues a SCSI bus rescan to find newly attached devices and remove missing ones.
        /// </summary>
        public void RescanBus()
        {
            try
            {
                NativeFileIO.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_SCSI_RESCAN_BUS,
                    null /* TODO Change to default(_) if this is not a reference type */, 0);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"IOCTL_SCSI_RESCAN_BUS failed: {ex.JoinMessages()}");
                API.RescanScsiAdapter(DeviceInstance);
            }
        }

        /// <summary>
        /// Re-enumerates partitions on all disk drives currently connected to this adapter. No
        /// exceptions are thrown on error, but any exceptions from underlying API calls are logged
        /// to trace log.
        /// </summary>
        public void UpdateDiskProperties()
        {
            foreach (var device in GetDeviceList())

                UpdateDiskProperties(device);
        }

        /// <summary>
        /// Re-enumerates partitions on specified disk currently connected to this adapter. No
        /// exceptions are thrown on error, but any exceptions from underlying API calls are logged
        /// to trace log.
        /// </summary>
        public bool UpdateDiskProperties(uint DeviceNumber)
        {
            try
            {
                using (var disk = OpenDevice(DeviceNumber, 0))
                {
                    if (!NativeFileIO.UpdateDiskProperties(disk.SafeFileHandle, throwOnFailure: false))
                    {
                        Trace.WriteLine(
                            $"Error updating disk properties for device {DeviceNumber}: {new Win32Exception().Message}");

                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error updating disk properties for device {DeviceNumber}: {ex.JoinMessages()}");

                return false;
            }
        }

        /// <summary>
        /// Opens a DiskDevice object for specified device number. Device numbers are created when
        /// a new virtual disk is created and returned in a reference parameter to CreateDevice
        /// method.
        /// </summary>
        public DiskDevice OpenDevice(uint DeviceNumber, FileAccess AccessMode)
        {
            try
            {
                var device_name = GetDeviceName(DeviceNumber);

                if (device_name == null)
                    throw new DriveNotFoundException($"No drive found for device number {DeviceNumber}");

                return new DiskDevice($@"\\?\{device_name}", AccessMode);
            }
            catch (Exception ex)
            {
                throw new DriveNotFoundException($"Device {DeviceNumber} is not ready", ex);
            }
        }

        /// <summary>
        /// Opens a DiskDevice object for specified device number. Device numbers are created when
        /// a new virtual disk is created and returned in a reference parameter to CreateDevice
        /// method. This overload requests a DiskDevice object without read or write access, that
        /// can only be used to query metadata such as size, geometry, SCSI address etc.
        /// </summary>
        public DiskDevice OpenDevice(uint DeviceNumber)
        {
            try
            {
                var device_name = GetDeviceName(DeviceNumber);

                if (device_name == null)
                    throw new DriveNotFoundException($"No drive found for device number {DeviceNumber}");

                return new DiskDevice($@"\\?\{device_name}");
            }
            catch (Exception ex)
            {
                throw new DriveNotFoundException($"Device {DeviceNumber} is not ready", ex);
            }
        }

        /// <summary>
        /// Returns a PhysicalDrive or CdRom device name for specified device number. Device numbers
        /// are created when a new virtual disk is created and returned in a reference parameter to
        /// CreateDevice method.
        /// </summary>
        public string GetDeviceName(uint DeviceNumber)
        {
            try
            {
                var raw_device = GetRawDeviceName(DeviceNumber);

                if (raw_device == null)
                    return null;

                return NativeFileIO.GetPhysicalDriveNameForNtDevice(raw_device);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error getting device name for device number {DeviceNumber}: {ex.JoinMessages()}");
                return null;
            }
        }

        /// <summary>
        /// Returns an NT device path to the physical device object that SCSI port driver has created for a mounted device.
        /// This device path can be used even if there is no functional driver attached to the device stack.
        /// </summary>
        public string GetRawDeviceName(uint DeviceNumber)
        {
            return API.EnumeratePhysicalDeviceObjectPaths(DeviceInstance, DeviceNumber).FirstOrDefault();
        }

        /// <summary>
        /// Returns a PnP registry property for the device object that SCSI port driver has created for a mounted device.
        /// </summary>
        public IEnumerable<string> GetPnPDeviceName(uint DeviceNumber, CmDevNodeRegistryProperty prop)
        {
            return API.EnumerateDeviceProperty(DeviceInstance, DeviceNumber, prop);
        }
    }

    /// <summary>
    /// Object storing properties for a virtual disk device. Returned by QueryDevice() method.
    /// </summary>
    public sealed class DeviceProperties
    {
        public DeviceProperties(ScsiAdapter adapter, UInt32 device_number)
        {
            DeviceNumber = device_number;

            adapter.QueryDevice(DeviceNumber, ref DiskSize, ref BytesPerSector, ref ImageOffset, ref Flags, ref Filename, ref WriteOverlayImageFile);
        }

        /// <summary>Device number of virtual disk.</summary>
        public UInt32 DeviceNumber { get; }

        /// <summary>Size of virtual disk.</summary>
        public Int64 DiskSize { get; }

        /// <summary>Number of bytes per sector for virtual disk geometry.</summary>
        public UInt32 BytesPerSector { get; }

        /// <summary>
        /// A skip offset if virtual disk data does not begin immediately at start of disk image file.
        /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
        /// or Windows filesystem drivers.
        /// </summary>
        public Int64 ImageOffset { get; }

        /// <summary>
        /// Flags specifying properties for virtual disk. See comments for each flag value.
        /// </summary>
        public DeviceFlags Flags { get; }

        /// <summary>
        /// Name of disk image file holding storage for file type virtual disk or used to create a virtual memory type virtual disk.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Path to differencing file used in write-temporary mode.
        /// </summary>
        public string WriteOverlayImageFile { get; }
    }

}
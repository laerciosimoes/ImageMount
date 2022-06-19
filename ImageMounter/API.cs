
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using ImageMounter.IO;
using ImageMounter.IO.Native;
using ImageMounter.IO.Native.Enum;
using ImageMounter.IO.Native.Struct;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter
{
    /// <summary>
    /// API for manipulating flag values, issuing SCSI bus rescans, manage write filter driver and similar tasks.
    /// </summary>
    [ComVisible(false)]
    public sealed class API
    {
        
        private API()
        {
        }

        public static Version OSVersion { get; } = NativeFileIO.GetOSVersion().Version;

        public static string Kernel { get; } = GetKernelName();

        public static bool HasStorPort { get; }

        private static string GetKernelName()
        {
            if (OSVersion >= new Version(10, 0))
            {
                Kernel = "Win10";
                HasStorPort = true;
            }
            else
                throw new NotSupportedException($"Unsupported Windows version ('{OSVersion}')");

            return Kernel;
        }

        /// <summary>
        /// Builds a list of device paths for active Arsenal Image Mounter objects.
        /// </summary>
        public static IEnumerable<string> EnumerateAdapterDevicePaths(IntPtr HwndParent)
        {
            IEnumerable<string> devinstances = null;
            var status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt", devinstances);

            if (status != 0 || devinstances == null)
            {
                Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status}");
                yield break;
            }

            foreach (var devinstname in devinstances)
            {
                using var DevInfoSet = UnsafeNativeMethods.SetupDiGetClassDevs(
                    NativeConstants.SerenumBusEnumeratorGuid, devinstname, HwndParent,
                    NativeConstants.DIGCF_DEVICEINTERFACE | NativeConstants.DIGCF_PRESENT);
                if (DevInfoSet.IsInvalid)
                    throw new Win32Exception();

                var i = 0U;
                do
                {
                    SP_DEVICE_INTERFACE_DATA DeviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                    DeviceInterfaceData.Initialize();
                    if (UnsafeNativeMethods.SetupDiEnumDeviceInterfaces(DevInfoSet, IntPtr.Zero,
                            NativeConstants.SerenumBusEnumeratorGuid, i, DeviceInterfaceData) == false)
                        break;
                    SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData =
                        new SP_DEVICE_INTERFACE_DETAIL_DATA();
                    DeviceInterfaceDetailData.Initialize();
                    if (UnsafeNativeMethods.SetupDiGetDeviceInterfaceDetail(DevInfoSet,
                            DeviceInterfaceData, DeviceInterfaceDetailData,
                            System.Convert.ToUInt32(Marshal.SizeOf(DeviceInterfaceData)), 0, IntPtr.Zero) == true)
                        yield return DeviceInterfaceDetailData.DevicePath;
                    i += 1U;
                } while (true);
            }
        }

        /// <summary>
        /// Returns a value indicating whether Arsenal Image Mounter driver is
        /// installed and running.
        /// </summary>
        public static bool AdapterDevicePresent
        {
            get
            {
                var devInsts = EnumerateAdapterDeviceInstanceNames();
                if (devInsts == null)
                    return false;
                else
                    return true;
            }
        }

        /// <summary>
        /// Builds a list of setup device ids for active Arsenal Image Mounter
        /// objects. Device ids are used in calls to plug-and-play setup functions.
        /// </summary>
        public static IEnumerable<UInt32> EnumerateAdapterDeviceInstances()
        {
            var devinstances = EnumerateAdapterDeviceInstanceNames();

            foreach (var devinstname in devinstances)
            {
                Trace.WriteLine($"Found adapter instance '{devinstname}'");

                var devInst = NativeFileIO.GetDevInst(devinstname);

                if (!devInst.HasValue)
                    continue;

                yield return devInst.Value;
            }
        }

        /// <summary>
        /// Builds a list of setup device ids for active Arsenal Image Mounter
        /// objects. Device ids are used in calls to plug-and-play setup functions.
        /// </summary>
        public static IEnumerable<string> EnumerateAdapterDeviceInstanceNames()
        {
            IEnumerable<string> devinstances = null;

            var status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt", devinstances);

            if (status != 0 || devinstances == null)
            {
                Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status}");
                return null;
            }

            return devinstances;
        }

        /// <summary>
        /// Issues a SCSI bus rescan on found Arsenal Image Mounter adapters. This causes Disk Management
        /// in Windows to find newly created virtual disks and remove newly deleted ones.
        /// </summary>
        public static bool RescanScsiAdapter(uint devInst)
        {
            bool rc;

            var status = UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0);
            if (status != 0)
                Trace.WriteLine($"Re-enumeration of '{devInst}' failed: 0x{status}");
            else
            {
                Trace.WriteLine($"Re-enumeration of '{devInst}' successful.");
                rc = true;
            }

            return rc;
        }

        private const string NonRemovableSuffix = ":$NonRemovable";

        public static IEnumerable<string> EnumeratePhysicalDeviceObjectPaths(UInt32 devinstAdapter, UInt32 DeviceNumber)
        {
            return from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter)
                let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
                where !string.IsNullOrWhiteSpace(path)
                let address = NativeFileIO.GetScsiAddressForNtDevice(path)
                where address.HasValue && address.Value.DWordDeviceNumber.Equals(DeviceNumber)
                select path;
        }

        public static IEnumerable<string> EnumerateDeviceProperty(UInt32 devinstAdapter, UInt32 DeviceNumber,
            CmDevNodeRegistryProperty prop)
        {
            ; /* 

        Return _
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)
            From value In NativeFileIO.GetDeviceRegistryProperty(devinstChild, prop)
            Select value

 */
            return new List<string>();
        }

        public static void UnregisterWriteOverlayImage(uint devInst)
        {
            RegisterWriteOverlayImage(devInst, OverlayImagePath: null, FakeNonRemovable: false);
        }

        public static void RegisterWriteOverlayImage(uint devInst, string OverlayImagePath)
        {
            RegisterWriteOverlayImage(devInst, OverlayImagePath, FakeNonRemovable: false);
        }

        public static void RegisterWriteOverlayImage(uint devInst, string OverlayImagePath, bool FakeNonRemovable)
        {
            string nativepath;

            if (!string.IsNullOrWhiteSpace(OverlayImagePath))
                nativepath = NativeFileIO.GetNtPath(OverlayImagePath);
            else
            {
                OverlayImagePath = null;
                nativepath = null;
            }

            var pdo_path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devInst);
            var dev_path = NativeFileIO.QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path))
                .FirstOrDefault();

            Trace.WriteLine(
                $"Device {pdo_path} devinst {devInst}. Registering write overlay '{nativepath}', FakeNonRemovable={FakeNonRemovable}");

            using (var regkey =
                   Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\aimwrfltr\Parameters"))
            {
                if (nativepath == null)
                    regkey.DeleteValue(pdo_path, throwOnMissingValue: false);
                else if (FakeNonRemovable)
                    regkey.SetValue(pdo_path, $"{nativepath}{NonRemovableSuffix}", RegistryValueKind.String);
                else
                    regkey.SetValue(pdo_path, nativepath, RegistryValueKind.String);
            }

            if (nativepath == null)
                NativeFileIO.RemoveFilter(devInst, "aimwrfltr");
            else
                NativeFileIO.AddFilter(devInst, "aimwrfltr");

            var last_error = 0;

            for (var r = 1; r <= 4; r++)
            {
                NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, devInst);

                WriteFilterStatistics statistics = new WriteFilterStatistics();

                last_error = GetWriteOverlayStatus(pdo_path, statistics);

                Trace.WriteLine(
                    $"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}");

                if (nativepath == null && last_error == NativeConstants.NO_ERROR)
                {
                    Trace.WriteLine("Filter driver not yet unloaded, retrying...");
                    Thread.Sleep(300);
                    continue;
                }
                else if (nativepath != null && (last_error == NativeConstants.ERROR_INVALID_FUNCTION ||
                                                last_error == NativeConstants.ERROR_INVALID_PARAMETER ||
                                                last_error == NativeConstants.ERROR_NOT_SUPPORTED))
                {
                    Trace.WriteLine("Filter driver not yet loaded, retrying...");
                    Thread.Sleep(300);
                    continue;
                }
                else if ((nativepath != null && last_error != NativeConstants.NO_ERROR) || (nativepath == null &&
                             last_error != NativeConstants.ERROR_INVALID_FUNCTION &&
                             last_error != NativeConstants.ERROR_INVALID_PARAMETER &&
                             last_error != NativeConstants.ERROR_NOT_SUPPORTED))
                    throw new NotSupportedException("Error checking write filter driver status",
                        new Win32Exception(last_error));
                else if ((nativepath != null && statistics.Initialized) || nativepath == null)
                    return;

                throw new IOException("Error adding write overlay to device",
                    NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode));
            }

            var in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path).Take(10)
                .Select(NativeFileIO.FormatProcessName).ToArray();

            if (in_use_apps.Length == 0 && last_error != 0)
                throw new NotSupportedException("Write filter driver not attached to device",
                    new Win32Exception(last_error));
            else if (in_use_apps.Length == 0)
                throw new NotSupportedException("Write filter driver not attached to device");
            else
            {
                var apps = string.Join($", ", in_use_apps);
                var message =
                    $@"Write filter driver cannot be attached while applications hold the disk device open. Currently, the following application hold the disk device open: {apps}";
                throw new UnauthorizedAccessException( message);
            }

            throw new FileNotFoundException("Error adding write overlay: Device not found.");
        }

        public static void RegisterWriteFilter(UInt32 devinstAdapter, UInt32 DeviceNumber,
            RegisterWriteFilterOperation operation)
        {
            foreach (var dev in from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter)
                     let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
                     where !string.IsNullOrWhiteSpace(path)
                     let address = NativeFileIO.GetScsiAddressForNtDevice(path)
                     where address.HasValue && address.Value.DWordDeviceNumber.Equals(DeviceNumber)
                     select devinstChild)
            {
                Trace.WriteLine(
                    $"Device number {DeviceNumber}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.");

                if (operation == RegisterWriteFilterOperation.Unregister)
                {
                    if (NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr"))
                        NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild);

                    return;
                }

                var last_error = 0;

                for (var r = 1; r <= 2; r++)
                {
                    if (NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr"))
                        NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild);

                    WriteFilterStatistics statistics = new WriteFilterStatistics();

                    last_error = GetWriteOverlayStatus(dev.path, statistics);

                    if (last_error == NativeConstants.ERROR_INVALID_FUNCTION)
                    {
                        Trace.WriteLine("Filter driver not loaded, retrying...");
                        Thread.Sleep(200);
                        continue;
                    }
                    else if (last_error != NativeConstants.NO_ERROR)
                        throw new NotSupportedException("Error checking write filter driver status",
                            new Win32Exception());

                    if (statistics.Initialized)
                        return;

                    throw new IOException("Error adding write overlay to device",
                        NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode));
                }

                var in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(dev.path).Take(10)
                    .Select(NativeFileIO.FormatProcessName).ToArray();

                if (in_use_apps.Length == 0 && last_error > 0)
                    throw new NotSupportedException("Write filter driver not attached to device",
                        new Win32Exception(last_error));
                else if (in_use_apps.Length == 0)
                    throw new NotSupportedException("Write filter driver not attached to device");
                else
                {
                    var apps = string.Join(", ", in_use_apps);
                    throw new UnauthorizedAccessException(
                        $@"Write filter driver cannot be attached while applications hold the virtual disk device open. Currently, the following application hold the disk device open: {apps}");
                }
            }

            throw new FileNotFoundException("Error adding write overlay: Device not found.");
        }


        public enum RegisterWriteFilterOperation
        {
            Register,
            Unregister
        }

        /// <summary>
        /// Retrieves status of write overlay for mounted device.
        /// </summary>
        /// <param name="NtDevicePath">NT path to device.</param>
        /// <param name="Statistics">Data structure that receives current statistics and settings for filter</param>
        /// <returns>Returns 0 on success or Win32 error code on failure</returns>
        public static int GetWriteOverlayStatus(string NtDevicePath, out WriteFilterStatistics Statistics)
        {
            using (var hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, 0, FileShare.ReadWrite,
                       NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0,
                       null /* TODO Change to default(_) if this is not a reference type */,
                       null /* TODO Change to default(_) if this is not a reference type */))
            {
                return GetWriteOverlayStatus(hDevice, Statistics);
            }
        }

        /// <summary>
        /// Retrieves status of write overlay for mounted device.
        /// </summary>
        /// <param name="hDevice">Handle to device.</param>
        /// <param name="Statistics">Data structure that receives current statistics and settings for filter</param>
        /// <returns>Returns 0 on success or Win32 error code on failure</returns>
        public static int GetWriteOverlayStatus(SafeFileHandle hDevice, out WriteFilterStatistics Statistics)
        {
            Statistics = WriteFilterStatistics.Initialize();

            if (UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_GET_DEVICE_DATA,
                    IntPtr.Zero, 0, Statistics, Statistics.Version,
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */))
                return NativeConstants.NO_ERROR;
            else
                return Marshal.GetLastWin32Error();
        }

        /// <summary>
        /// Deletes the write overlay image file after use. Also sets this filter driver to
        /// silently ignore flush requests to improve performance when integrity of the write
        /// overlay image is not needed for future sessions.
        /// </summary>
        /// <param name="NtDevicePath">NT path to device.</param>
        /// <returns>Returns 0 on success or Win32 error code on failure</returns>
        public static int SetWriteOverlayDeleteOnClose(string NtDevicePath)
        {
            using (var hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, FileAccess.ReadWrite, FileShare.ReadWrite,
                       NativeStruct.NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0,
                       null ,
                       null ))
            {
                return SetWriteOverlayDeleteOnClose(hDevice);
            }
        }

        /// <summary>
        /// Deletes the write overlay image file after use. Also sets this filter driver to
        /// silently ignore flush requests to improve performance when integrity of the write
        /// overlay image is not needed for future sessions.
        /// </summary>
        /// <param name="hDevice">Handle to device.</param>
        /// <returns>Returns 0 on success or Win32 error code on failure</returns>
        public static int SetWriteOverlayDeleteOnClose(SafeFileHandle hDevice)
        {
            if (UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_DELETE_ON_CLOSE,
                    IntPtr.Zero, 0, IntPtr.Zero, 0, ref default(uint), default(IntPtr)))
                return NativeConstants.NO_ERROR;
            else
                return Marshal.GetLastWin32Error();
        }

        private sealed class UnsafeNativeMethods
        {
            private UnsafeNativeMethods()
            {
            }

            public const UInt32 IOCTL_AIMWRFLTR_GET_DEVICE_DATA = 0x88443404U;

            public const UInt32 IOCTL_AIMWRFLTR_DELETE_ON_CLOSE = 0x8844F407U;

            [System.Runtime.InteropServices.DllImport("kernel32")]
            public static extern bool DeviceIoControl(SafeFileHandle hDevice, UInt32 dwIoControlCode, IntPtr lpInBuffer,
                UInt32 nInBufferSize, out WriteFilterStatistics lpOutBuffer, UInt32 nOutBufferSize,
                out UInt32 lpBytesReturned, IntPtr lpOverlapped);

            [System.Runtime.InteropServices.DllImport("kernel32")]
            public static extern bool DeviceIoControl(SafeFileHandle hDevice, UInt32 dwIoControlCode, IntPtr lpInBuffer,
                UInt32 nInBufferSize, IntPtr lpOutBuffer, UInt32 nOutBufferSize, out UInt32 lpBytesReturned,
                IntPtr lpOverlapped);
        }
    }

}
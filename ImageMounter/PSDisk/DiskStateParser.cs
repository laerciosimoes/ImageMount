using System.Diagnostics;
using System.Runtime.Versioning;
using ImageMounter.Interop.IO;
using ImageMounter.IO;

namespace ImageMounter.PSDisk
{
    public static class DiskStateParser
    {
        public static IEnumerable<DiskStateView> GetSimpleView(ScsiAdapter portnumber, IEnumerable<DeviceProperties> deviceProperties)
        {
            return GetSimpleViewSpecial<DiskStateView>(portnumber, deviceProperties);
        }

        public static IEnumerable<T> GetSimpleViewSpecial<T>(ScsiAdapter portnumber, IEnumerable<DeviceProperties> deviceProperties) where T : new(), DiskStateView
        {
            try
            {
                /*
                 * TODO : Refactor
                 */
                /*
                var ids = NativeFileIO.GetDevicesScsiAddresses(portnumber);

                var getid = DeviceProperties dev =>
                {
                    string result = null;
                    if (ids.TryGetValue(dev.DeviceNumber, result))
                        return result;
                    else
                    {
                        Trace.WriteLine($"No PhysicalDrive object found for device number {dev.DeviceNumber}");
                        return null;
                    }
                };

                return deviceProperties.Select(dev =>
                {
                    T view = new T()
                    {
                        DeviceProperties = dev,
                        DeviceName = getid(dev)
                    };

                    view.FakeDiskSignature = dev.Flags.HasFlag(DeviceFlags.FakeDiskSignatureIfZero);

                    if (view.DeviceName != null)
                    {
                        try
                        {
                            view.DevicePath = $@"\\?\{view.DeviceName}";
                            using (DiskDevice device = new DiskDevice(view.DevicePath, FileAccess.Read))
                            {
                                view.RawDiskSignature = device.DiskSignature;
                                view.NativePropertyDiskOffline = device.DiskPolicyOffline;
                                view.NativePropertyDiskReadOnly = device.DiskPolicyReadOnly;
                                view.StorageDeviceNumber = device.StorageDeviceNumber;
                                var drive_layout = device.DriveLayoutEx;
                                view.DiskId = drive_layout as DriveLayoutInformationGPT?.GPT.DiskId;
                                if (device.HasValidPartitionTable)
                                    view.NativePartitionLayout = drive_layout?.DriveLayoutInformation.PartitionStyle;
                                else
                                    view.NativePartitionLayout = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Error reading signature from MBR for drive {view.DevicePath}: {ex.JoinMessages()}");
                        }

                        try
                        {
                            view.Volumes = NativeFileIO.EnumerateDiskVolumes(view.DevicePath).ToArray();
                            view.MountPoints = view.Volumes?.SelectMany(NativeFileIO.EnumerateVolumeMountPoints).ToArray();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Error enumerating volumes for drive {view.DevicePath}: {ex.JoinMessages()}");
                        }
                    }

                    return view;
                });
                */
                return new List<T>();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception in GetSimpleView: {ex}");

                throw new Exception("Exception generating view", ex);
            }
        }
    }
}

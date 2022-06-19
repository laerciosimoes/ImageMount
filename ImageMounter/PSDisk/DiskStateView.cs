using System.ComponentModel;
using ImageMounter.IO;
using ImageMounter.IO.Native.Enum;
using ImageMounter.IO.Native.Struct;

namespace ImageMounter.PSDisk
{
    public class DiskStateView : INotifyPropertyChanged
    {
        public DiskStateView()
        {
        }

        private bool _DetailsVisible;
        private bool _Selected;
        private string _ImagePath;
        private long? _DiskSizeNumeric;

        public DeviceProperties DeviceProperties { get; set; }

        public UInt32? RawDiskSignature { get; set; }

        public Guid? DiskId { get; set; }

        public string DevicePath { get; set; }

        public string DeviceName { get; set; }

        public STORAGE_DEVICE_NUMBER? StorageDeviceNumber { get; set; }

        public string DriveNumberString
        {
            get
            {
                return StorageDeviceNumber?.DeviceNumber.ToString();
            }
        }

        public string ScsiId
        {
            get
            {
                if (DeviceProperties != null)
                    return DeviceProperties.DeviceNumber.ToString("X6");
                else
                    return "N/A";
            }
        }

        public string ImagePath
        {
            get
            {
                return _ImagePath ?? DeviceProperties?.Filename;
            }
            set
            {
                _ImagePath = Value;
            }
        }

        public bool? NativePropertyDiskOffline { get; set; }

        public bool? IsOffline
        {
            get
            {
                return NativePropertyDiskOffline;
            }
        }

        public string OfflineString
        {
            get
            {
                var state = IsOffline;
                if (!state.HasValue)
                    return "N/A";
                else if (state.Value)
                    return "Offline";
                else
                    return "Online";
            }
        }

        public PARTITION_STYLE? NativePartitionLayout { get; set; }

        public string PartitionLayout
        {
            get
            {
                if (NativePartitionLayout.HasValue)
                {
                    switch (NativePartitionLayout.Value)
                    {
                        case PARTITION_STYLE.GPT:
                        {
                            return "GPT";
                        }

                        case PARTITION_STYLE.MBR:
                        {
                            return "MBR";
                        }

                        case PARTITION_STYLE.RAW:
                        {
                            return "RAW";
                        }

                        default:
                        {
                            return "Unknown";
                        }
                    }
                }
                else
                    return "None";
            }
        }

        public string Signature
        {
            get
            {
                if (DiskId.HasValue)
                    return DiskId.Value.ToString("b");
                else if (RawDiskSignature.HasValue && (FakeDiskSignature || FakeMBR))
                    return $"{RawDiskSignature} (faked)";
                else if (RawDiskSignature.HasValue)
                    return RawDiskSignature.Value.ToString("X8");
                else
                    return "N/A";
            }
        }

        public long? DiskSizeNumeric
        {
            get
            {
                if (_DiskSizeNumeric.HasValue)
                    return _DiskSizeNumeric;
                else if (DeviceProperties != null)
                    return DeviceProperties.DiskSize;
                else
                    return default(Long?);
            }
            set
            {
                _DiskSizeNumeric = Value;
            }
        }

        public string DiskSize
        {
            get
            {
                var size = DiskSizeNumeric;
                if (!size.HasValue)
                    return null;

                return System.IO.FormatBytes(size.Value);
            }
        }

        public bool? NativePropertyDiskReadOnly { get; set; }

        public bool FakeDiskSignature { get; set; }

        public bool FakeMBR { get; set; }

        public string[] Volumes { get; set; }

        public string VolumesString
        {
            get
            {
                if (Volumes == null)
                    return null;
                return string.Join(Environment.NewLine, Volumes);
            }
        }

        public string[] MountPoints { get; set; }

        public string MountPointsString
        {
            get
            {
                if (MountPoints == null || MountPoints.Length == 0)
                    return string.Empty;

                return string.Join(Environment.NewLine, MountPoints);
            }
        }

        public string MountPointsSequenceString
        {
            get
            {
                if (MountPoints == null || MountPoints.Length == 0)
                    return string.Empty;

                return $"Mount Points: {string.Join(", ", MountPoints)}";
            }
        }

        public bool? IsReadOnly
        {
            get
            {
                if (NativePropertyDiskReadOnly.HasValue)
                    return NativePropertyDiskReadOnly.Value;
                else
                    return DeviceProperties?.Flags.HasFlag(DeviceFlags.ReadOnly);
            }
        }

        public string ReadOnlyString
        {
            get
            {
                var state = IsReadOnly;
                if (!state.HasValue)
                    return null;
                else if (state.Value)
                    return "RO";
                else
                    return "RW";
            }
        }

        public string ReadWriteString
        {
            get
            {
                var state = IsReadOnly;
                if (!state.HasValue)
                    return null;
                else if (state.Value)
                    return "Read only";
                else
                    return "Read write";
            }
        }

        public bool DetailsVisible
        {
            get
            {
                return _DetailsVisible;
            }
            set
            {
                if (!_DetailsVisible == Value)
                {
                    _DetailsVisible = Value;
                    NotifyPropertyChanged("DetailsVisible");
                    NotifyPropertyChanged("DetailsHidden");
                }
            }
        }

        public bool DetailsHidden
        {
            get
            {
                return !_DetailsVisible;
            }
            set
            {
                if (_DetailsVisible == Value)
                {
                    _DetailsVisible = !Value;
                    NotifyPropertyChanged("DetailsVisible");
                    NotifyPropertyChanged("DetailsHidden");
                }
            }
        }

        public bool Selected
        {
            get
            {
                return _Selected;
            }
            set
            {
                if (!_Selected == Value)
                {
                    _Selected = Value;
                    NotifyPropertyChanged("Selected");
                }
            }
        }

        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public delegate void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e);
    }
}

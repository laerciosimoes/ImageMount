using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.IO.Native
{
    public abstract class NativeConstants
    {
        public const uint STANDARD_RIGHTS_REQUIRED = 0xF0000U;

        public const uint FILE_ATTRIBUTE_NORMAL = 0x80U;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000U;
        public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000U;
        public const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x200000U;
        public const uint OPEN_ALWAYS = 4U;
        public const uint OPEN_EXISTING = 3U;
        public const uint CREATE_ALWAYS = 2U;
        public const uint CREATE_NEW = 1U;
        public const uint TRUNCATE_EXISTING = 5U;
        public const uint EVENT_QUERY_STATE = 1U;
        public const uint EVENT_MODIFY_STATE = 2U;


        public const uint EVENT_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | FileSystemRights.Synchronize | FileSystemRights.ReadData | FileSystemRights.WriteData;

        public const uint NO_ERROR = 0U;
        public const uint ERROR_INVALID_FUNCTION = 1U;
        public const uint ERROR_IO_DEVICE = 0x45DU;
        public const uint ERROR_FILE_NOT_FOUND = 2U;
        public const uint ERROR_PATH_NOT_FOUND = 3U;
        public const uint ERROR_ACCESS_DENIED = 5U;
        public const uint ERROR_NO_MORE_FILES = 18U;
        public const uint ERROR_HANDLE_EOF = 38U;
        public const uint ERROR_NOT_SUPPORTED = 50U;
        public const uint ERROR_DEV_NOT_EXIST = 55U;
        public const uint ERROR_INVALID_PARAMETER = 87U;
        public const uint ERROR_MORE_DATA = 0x234U;
        public const uint ERROR_NOT_ALL_ASSIGNED = 1300U;
        public const uint ERROR_INSUFFICIENT_BUFFER = 122U;
        public const uint ERROR_IN_WOW64 = 0xE0000235;

        public const uint FSCTL_GET_COMPRESSION = 0x9003;
        public const uint FSCTL_SET_COMPRESSION = 0x9C040;
        public const ushort COMPRESSION_FORMAT_NONE = 0;
        public const ushort COMPRESSION_FORMAT_DEFAULT = 1;
        public const uint FSCTL_SET_SPARSE = 0x900C4;
        public const uint FSCTL_GET_RETRIEVAL_POINTERS = 0x90073;
        public const uint FSCTL_ALLOW_EXTENDED_DASD_IO = 0x90083;

        public const uint FSCTL_LOCK_VOLUME = 0x90018;
        public const uint FSCTL_DISMOUNT_VOLUME = 0x90020;

        public const uint FSCTL_SET_REPARSE_POINT = 0x900A4;
        public const uint FSCTL_GET_REPARSE_POINT = 0x900A8;
        public const uint FSCTL_DELETE_REPARSE_POINT = 0x900A;
        public const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003U;

        public const uint IOCTL_SCSI_MINIPORT = 0x4D008;
        public const uint IOCTL_SCSI_GET_ADDRESS = 0x41018;
        public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
        public const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;
        public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x70000;
        public const uint IOCTL_DISK_GET_LENGTH_INFO = 0x7405;
        public const uint IOCTL_DISK_GET_PARTITION_INFO = 0x74004;
        public const uint IOCTL_DISK_GET_PARTITION_INFO_EX = 0x70048;
        public const uint IOCTL_DISK_GET_DRIVE_LAYOUT = 0x7400;
        public const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x70050;
        public const uint IOCTL_DISK_SET_DRIVE_LAYOUT_EX = 0x7C054;
        public const uint IOCTL_DISK_CREATE_DISK = 0x7C058;
        public const uint IOCTL_DISK_GROW_PARTITION = 0x7C0D0;
        public const uint IOCTL_DISK_UPDATE_PROPERTIES = 0x70140;
        public const uint IOCTL_DISK_IS_WRITABLE = 0x70024;
        public const uint IOCTL_SCSI_RESCAN_BUS = 0x4101;

        public const uint IOCTL_DISK_GET_DISK_ATTRIBUTES = 0x700F0;
        public const uint IOCTL_DISK_SET_DISK_ATTRIBUTES = 0x7C0F4;
        public const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x560000;
        public const uint IOCTL_VOLUME_OFFLINE = 0x56C00;
        public const uint IOCTL_VOLUME_ONLINE = 0x56C008;

        public const uint FILE_DEVICE_DISK = 0x7;

        public const int ERROR_WRITE_PROTECT = 19;
        public const int ERROR_NOT_READY = 21;
        public const uint FVE_E_LOCKED_VOLUME = 0x80310000;

        public const uint SC_MANAGER_CREATE_SERVICE = 0x2;
        public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        public const uint SERVICE_KERNEL_DRIVER = 0x1;
        public const uint SERVICE_FILE_SYSTEM_DRIVER = 0x2;
        public const uint SERVICE_WIN32_OWN_PROCESS = 0x10; // Service that runs in its own process. 
        public const uint SERVICE_WIN32_INTERACTIVE = 0x100; // Service that runs in its own process. 
        public const uint SERVICE_WIN32_SHARE_PROCESS = 0x20;

        public const uint SERVICE_BOOT_START = 0x0;
        public const uint SERVICE_SYSTEM_START = 0x1;
        public const uint SERVICE_AUTO_START = 0x2;
        public const uint SERVICE_DEMAND_START = 0x3;
        public const uint SERVICE_ERROR_IGNORE = 0x0;
        public const uint SERVICE_CONTROL_STOP = 0x1;
        public const uint ERROR_SERVICE_DOES_NOT_EXIST = 1060;
        public const uint ERROR_SERVICE_ALREADY_RUNNING = 1056;

        public const uint DIGCF_DEFAULT = 0x1;
        public const uint DIGCF_PRESENT = 0x2;
        public const uint DIGCF_ALLCLASSES = 0x4;
        public const uint DIGCF_PROFILE = 0x8;
        public const uint DIGCF_DEVICEINTERFACE = 0x10;

        public const uint DRIVER_PACKAGE_DELETE_FILES = 0x20U;
        public const uint DRIVER_PACKAGE_FORCE = 0x4U;
        public const uint DRIVER_PACKAGE_SILENT = 0x2U;

        public const uint CM_GETIDLIST_FILTER_SERVICE = 0x2U;

        public const uint DIF_PROPERTYCHANGE = 0x12;
        public const uint DICS_FLAG_CONFIGSPECIFIC = 0x2;  // ' make change in specified profile only
        public const uint DICS_PROPCHANGE = 0x3;

        public const uint CR_SUCCESS = 0x0;
        public const uint CR_FAILURE = 0x13;
        public const uint CR_NO_SUCH_VALUE = 0x25;
        public const uint CR_NO_SUCH_REGISTRY_KEY = 0x2E;

        public static Guid SerenumBusEnumeratorGuid { get; } = new Guid("{4D36E97B-E325-11CE-BFC1-08002BE10318}");
        public static Guid DiskDriveGuid { get; } = new Guid("{4D36E967-E325-11CE-BFC1-08002BE10318}");

        public static Guid DiskClassGuid { get; } = new Guid("{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}");
        public static Guid CdRomClassGuid { get; } = new Guid("{53F56308-B6BF-11D0-94F2-00A0C91EFB8B}");
        public static Guid StoragePortClassGuid { get; } = new Guid("{2ACCFE60-C130-11D2-B082-00A0C91EFB8B}");
        public static Guid ComPortClassGuid { get; } = new Guid("{86E0D1E0-8089-11D0-9CE4-08003E301F73}");

        public const string SE_BACKUP_NAME = "SeBackupPrivilege";
        public const string SE_RESTORE_NAME = "SeRestorePrivilege";
        public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
        public const string SE_DEBUG_NAME = "SeDebugPrivilege";
        public const string SE_TCB_NAME = "SeTcbPrivilege";
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

        public const uint PROCESS_DUP_HANDLE = 0x40;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public const uint TOKEN_QUERY = 0x8;
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x20;

        public const int KEY_READ = 0x20019;
        public const int REG_OPTION_BACKUP_RESTORE = 0x4;

        public const int SE_PRIVILEGE_ENABLED = 0x2;

        public const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
        public const uint STATUS_BUFFER_TOO_SMALL = 0xC0000023;
        public const uint STATUS_BUFFER_OVERFLOW = 0x80000005;
        public const uint STATUS_OBJECT_NAME_NOT_FOUND = 0xC0000034;
        public const uint STATUS_BAD_COMPRESSION_BUFFER = 0xC0000242;

        public const int FILE_BEGIN = 0;
        public const int FILE_CURRENT = 1;
        public const int FILE_END = 2;

        public static ReadOnlyMemory<byte> DefaultBootCode { get; } = new byte[] { 0xF4, 0xEB, 0xFD };   // HLT ; JMP -3

        private NativeConstants()
        {
        }
    }
}

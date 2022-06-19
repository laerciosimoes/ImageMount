using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using ImageMounter;
using ImageMounter.Interop;
using ImageMounter.Interop.IO;
using ImageMounter.Interop.Struct;
using ImageMounter.IO;
using ImageMounter.IO.Native;
using ImageMounter.IO.Native.Enum;
using ImageMounter.IO.Native.Struct;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace ImageMounter.IO
{

    /// <summary>
    /// Provides wrappers for Win32 file API. This makes it possible to open everything that CreateFile() can open and get a FileStream based .NET wrapper around the file handle.
    /// </summary>
    public sealed class NativeFileIO
    {
        
        /// <summary>
        ///  Encapsulates call to a Win32 API function that returns a BOOL value indicating success
        ///  or failure and where an error value is available through a call to GetLastError() in case
        ///  of failure. If value True is passed to this method it does nothing. If False is passed,
        ///  it calls GetLastError(), converts error code to a HRESULT value and throws a managed
        ///  exception for that HRESULT.
        ///  </summary>
        ///  <param name="result">Return code from a Win32 API function call.</param>
        public static void Win32Try(bool result)
        {
            if (result == false)
                throw new Win32Exception();
        }

        /// <summary>
        /// Encapsulates call to a Win32 API function that returns a value where failure
        /// is indicated as a NULL return and GetLastError() returns an error code. If
        /// non-zero value is passed to this method it just returns that value. If zero
        /// value is passed, it calls GetLastError() and throws a managed exception for
        /// that error code.
        /// </summary>
        /// <param name="result">Return code from a Win32 API function call.</param>
        public static T Win32Try<T>(T result)
        {
            if (result == null)
                throw new Win32Exception();
            return result;
        }

        /// <summary>
        /// Encapsulates call to an ntdll.dll API function that returns an NTSTATUS value indicating
        /// success or error status. If result is zero or positive, this function just passes through
        /// that value as return value. If result is negative indicating an error, it converts error
        /// code to a Win32 error code and throws a managed exception for that error code.
        /// </summary>
        /// <param name="result">Return code from a ntdll.dll API function call.</param>
        public static int NtDllTry(int result)
        {
            if (result < 0)
                throw new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(result));

            return result;
        }

        public static bool OfflineDiskVolumes(string device_path, bool force)
        {
            return OfflineDiskVolumes(device_path, force, CancellationToken.None);
        }


        public static async Task<bool> OfflineDiskVolumesAsync(string device_path, bool force, CancellationToken cancel)
        {
            var refresh = false;

            foreach (var volume in EnumerateDiskVolumes(device_path))
            {
                cancel.ThrowIfCancellationRequested();

                try
                {
                    using (DiskDevice device = new DiskDevice(volume.TrimEnd('\\'), FileAccess.ReadWrite))
                    {
                        if (device.IsDiskWritable && !device.DiskPolicyReadOnly.GetValueOrDefault())
                        {
                            Task t ;

                            try
                            {
                                device.FlushBuffers();
                                await device.DismountVolumeFilesystemAsync(Force: false, cancel)
                                    .ConfigureAwait(continueOnCapturedContext: false);
                            }
                            catch (Win32Exception ex) when
                                ((ex.NativeErrorCode == NativeConstants.ERROR_WRITE_PROTECT ||
                                  ex.NativeErrorCode == NativeConstants.ERROR_NOT_READY ||
                                  ex.NativeErrorCode == NativeConstants.ERROR_DEV_NOT_EXIST))
                            {
                                t = device.DismountVolumeFilesystemAsync(Force: true, cancel);
                            }

                            if (t != null)
                                await t.ConfigureAwait(continueOnCapturedContext: false);
                        }
                        else
                            await device.DismountVolumeFilesystemAsync(Force: true, cancel)
                                .ConfigureAwait(continueOnCapturedContext: false);

                        device.SetVolumeOffline(true);
                    }

                    refresh = true;

                    continue;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to safely dismount volume '{volume}': {ex.JoinMessages()}");

                    if (!force)
                    {
                        var dev_paths = QueryDosDevice(volume.Substring(4, 44)).ToArray();
                        var in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10)
                            .Select(FormatProcessName).ToArray();

                        if (in_use_apps.Length > 1)
                            throw new IOException(
                                $@"Failed to safely dismount volume '{volume}'. Currently, the following applications have files open on this volume: {string.Join(", ", in_use_apps)}",
                                ex);
                        else if (in_use_apps.Length == 1)
                            throw new IOException(
                                $@"Failed to safely dismount volume '{volume}'. Currently, the following application has files open on this volume:{in_use_apps(0)}",
                                ex);
                        else
                            throw new IOException($"Failed to safely dismount volume '{volume}'", ex);
                    }
                }

                cancel.ThrowIfCancellationRequested();

                try
                {
                    using (DiskDevice device = new DiskDevice(volume.TrimEnd('\\'), FileAccess.ReadWrite))
                    {
                        device.FlushBuffers();
                        await device.DismountVolumeFilesystemAsync(true, cancel)
                            .ConfigureAwait(continueOnCapturedContext: false);
                        device.SetVolumeOffline(true);
                    }

                    refresh = true;
                    continue;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to forcefully dismount volume '{volume}': {ex.JoinMessages()}");
                }

                return false;

            }

            return refresh;
        }

     
        public static void ShutdownSystem(ShutdownFlags Flags, ShutdownReasons Reason)
        {
            EnablePrivileges(NativeConstants.SE_SHUTDOWN_NAME);

            Win32Try(UnsafeNativeMethods.ExitWindowsEx(Flags, Reason));
        }

        public static string[] EnablePrivileges(params string[] privileges)
        {
            SafeFileHandle token ;
            if (!UnsafeNativeMethods.OpenThreadToken(UnsafeNativeMethods.GetCurrentThread(),
                    NativeConstants.TOKEN_ADJUST_PRIVILEGES | NativeConstants.TOKEN_QUERY,
                    openAsSelf: true, token))
                Win32Try(UnsafeNativeMethods.OpenProcessToken(
                    UnsafeNativeMethods.GetCurrentProcess(),
                    NativeConstants.TOKEN_ADJUST_PRIVILEGES | NativeConstants.TOKEN_QUERY,
                    token));

            using (token)
            {
                var intsize = System.Convert.ToInt64(PinnedBuffer<int>.TypeSize);
                var structsize = PinnedBuffer<LUID_AND_ATTRIBUTES>.TypeSize;

                Dictionary<string, LUID_AND_ATTRIBUTES> luid_and_attribs_list =
                    new Dictionary<string, LUID_AND_ATTRIBUTES>(privileges.Length);

                var luid_and_attribs = new LUID_AND_ATTRIBUTES()
                {
                    Attributes = NativeConstants.SE_PRIVILEGE_ENABLED
                };
                foreach (var privilege in privileges)
                {
                    if (UnsafeNativeMethods.LookupPrivilegeValue(null, privilege, luid_and_attribs.LUID))
                    {
                        luid_and_attribs_list.Add(privilege, luid_and_attribs);
                    }
                }

                if (luid_and_attribs_list.Count == 0) return null;

                using (PinnedBuffer<byte> buffer =
                       new PinnedBuffer<byte>(System.Convert.ToInt32(intsize + privileges.LongLength * structsize)))
                {
                    buffer.Write(0, luid_and_attribs_list.Count);

                    buffer.WriteArray(System.Convert.ToUInt64(intsize), luid_and_attribs_list.Values.ToArray(), 0,
                        luid_and_attribs_list.Count);

                    var rc = UnsafeNativeMethods.AdjustTokenPrivileges(token, false, buffer,
                        System.Convert.ToInt32(buffer.ByteLength), buffer,
                        null /* TODO Change to default(_) if this is not a reference type */);

                    var err = Marshal.GetLastWin32Error();

                    if (!rc)
                        throw new Win32Exception();

                    if (err == NativeConstants.ERROR_NOT_ALL_ASSIGNED)
                    {
                        var count = buffer.Read<int>(0);

                        var enabled_luids = new LUID_AND_ATTRIBUTES[count];


                        buffer.ReadArray(System.Convert.ToUInt64(intsize), enabled_luids, 0, count);
                        /*
                         * TODO : Refactor
                         */
                        var enabled_privileges = enabled_luids.ToArray()();
                            
                            /*
                            Aggregate enabled_luid In enabled_luids Join privilege_name
                            In luid_and_attribs_list On enabled_luid.LUID Equals privilege_name.Value.LUID
                            Select privilege_name.Key Into ToArray();
                            */

                        return enabled_privileges;
                    }

                    return privileges;
                }
            }
        }
        
        public static WaitHandle CreateWaitHandle(IntPtr Handle, bool inheritable)
        {
            SafeWaitHandle new_handle = null;

            var current_process = UnsafeNativeMethods.GetCurrentProcess();

            if (!UnsafeNativeMethods.DuplicateHandle(current_process, Handle, current_process, new_handle,
                    0, inheritable, 0x2))
                throw new Win32Exception();

            return new NativeWaitHandle(new_handle);
        }

        public static void SetEvent(SafeWaitHandle handle)
        {
            Win32Try(UnsafeNativeMethods.SetEvent(handle));
        }

        public static void SetInheritable(SafeHandle handle, bool inheritable)
        {
            Win32Try(UnsafeNativeMethods.SetHandleInformation(handle, 1U, inheritable ? 1U : 0U));
        }

        public static void SetProtectFromClose(SafeHandle handle, bool protect_from_close)
        {
            Win32Try(UnsafeNativeMethods.SetHandleInformation(handle, 2U, protect_from_close ? 2U : 0U));
        }


        /// <summary>
        ///  Returns current system handle table.
        ///  </summary>
        public static SystemHandleTableEntryInformation[] GetSystemHandleTable()
        {
            using (HGlobalBuffer buffer = new HGlobalBuffer(65536))
            {
                do
                {
                    var status = UnsafeNativeMethods.NtQuerySystemInformation(
                        SystemInformationClass.SystemHandleInformation, buffer,
                        System.Convert.ToInt32(buffer.ByteLength),
                        null /* TODO Change to default(_) if this is not a reference type */);
                    if (status == NativeConstants.STATUS_INFO_LENGTH_MISMATCH)
                    {
                        buffer.Resize((IntPtr)buffer.ByteLength << 1);
                        continue;
                    }

                    NtDllTry(status);
                    break;
                } while (true);

                var handlecount = buffer.Read<int>(0);
                var arrayoffset = IntPtr.Size;

                var array = SystemHandleTableEntryInformation[handlecount];

                buffer.ReadArray(System.Convert.ToUInt64(arrayoffset), array, 0, handlecount);

                return array;
            }
        }

    
        public static long LastObjectNameQuueryTime;

        public static uint LastObjectNameQueryGrantedAccess;

        /// <summary>
        ///  Enumerates open handles in the system.
        ///  </summary>
        ///  <param name="filterObjectType">Name of object types to return in the enumeration. Normally set to for example "File" to return file handles or "Key" to return registry key handles</param>
        ///  <returns>Enumeration with information about each handle table entry</returns>
        public static IEnumerable<HandleTableEntryInformation> EnumerateHandleTableHandleInformation(string filterObjectType)
        {
            return EnumerateHandleTableHandleInformation(GetSystemHandleTable(), filterObjectType);
        }

        private static readonly ConcurrentDictionary<byte, string> _objectTypes = new ConcurrentDictionary<byte, string>();

        private static IEnumerable<HandleTableEntryInformation> EnumerateHandleTableHandleInformation(
            IEnumerable<SystemHandleTableEntryInformation> handleTable, string filterObjectType)
        {
            handleTable.NullCheck(nameof(handleTable));

            if (filterObjectType != null)
                filterObjectType = string.Intern(filterObjectType);

            using (HGlobalBuffer buffer = new HGlobalBuffer(65536))
            {
                using (var processHandleList = new DisposableDictionary<int, SafeFileHandle>())
                {
                    using (var processInfoList = new DisposableDictionary<int, Process>())
                    {
                        Array.ForEach(Process.GetProcesses(), p => processInfoList.Add(p.Id, p));

                        foreach (var handle in handleTable)
                        {
                            string object_type;
                            string object_name;
                            Process processInfo;

                            if (handle.ProcessId == 0 ||
                                (filterObjectType != null && _objectTypes.TryGetValue(handle.ObjectType, object_type) &&
                                 !Object.ReferenceEquals(object_type, filterObjectType)) ||
                                !processInfoList.TryGetValue(handle.ProcessId, processInfo))
                                continue;

                            SafeFileHandle processHandle ;
                            if (!processHandleList.TryGetValue(handle.ProcessId, processHandle))
                            {
                                processHandle = UnsafeNativeMethods.OpenProcess(
                                    NativeConstants.PROCESS_DUP_HANDLE |
                                    NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION, false,
                                    handle.ProcessId);
                                if (processHandle.IsInvalid)
                                    processHandle = null;
                                processHandleList.Add(handle.ProcessId, processHandle);
                            }

                            if (processHandle == null)
                                continue;

                            SafeFileHandle duphandle = new SafeFileHandle(default(IntPtr), true);
                            var status = UnsafeNativeMethods.NtDuplicateObject(processHandle,
                                new IntPtr(handle.Handle), UnsafeNativeMethods.GetCurrentProcess(),
                                duphandle,
                                0, 0,
                                0);
                            if (status < 0)
                                continue;

                            try
                            {
                                int newbuffersize;

                                if (object_type == null)
                                {
                                    object_type = _objectTypes.GetOrAdd(handle.ObjectType, () =>
                                    {
                                        do
                                        {
                                            var rc = UnsafeNativeMethods.NtQueryObject(duphandle,
                                                ObjectInformationClass.ObjectTypeInformation, buffer,
                                                System.Convert.ToInt32(buffer.ByteLength), newbuffersize);
                                            if (rc == NativeConstants.STATUS_BUFFER_TOO_SMALL ||
                                                rc == NativeConstants.STATUS_BUFFER_OVERFLOW)
                                            {
                                                buffer.Resize(newbuffersize);
                                                continue;
                                            }
                                            else if (rc < 0)
                                                return null;

                                            break;
                                        } while (true);

                                        return string.Intern(buffer.Read<UNICODE_STRING>(0).ToString());
                                    });
                                }

                                if (object_type == null || (filterObjectType != null &&
                                                            !Object.ReferenceEquals(filterObjectType, object_type)))
                                    continue;

                                if (handle.GrantedAccess != 0x12019F && handle.GrantedAccess != 0x12008D &&
                                    handle.GrantedAccess != 0x120189 && handle.GrantedAccess != 0x16019F &&
                                    handle.GrantedAccess != 0x1A0089 && handle.GrantedAccess != 0x1A019F &&
                                    handle.GrantedAccess != 0x120089 && handle.GrantedAccess != 0x100000)
                                {
                                    do
                                    {
                                        LastObjectNameQueryGrantedAccess = handle.GrantedAccess;
                                        LastObjectNameQuueryTime = SafeNativeMethods.GetTickCount64();
                                        status = UnsafeNativeMethods.NtQueryObject(duphandle,
                                            ObjectInformationClass.ObjectNameInformation, buffer,
                                            System.Convert.ToInt32(buffer.ByteLength), newbuffersize);
                                        LastObjectNameQuueryTime = 0;
                                        if (status < 0 && newbuffersize > buffer.ByteLength)
                                        {
                                            buffer.Resize(newbuffersize);
                                            continue;
                                        }
                                        else if (status < 0)
                                            continue;

                                        break;
                                    } while (true);

                                    var name = buffer.Read<UNICODE_STRING>(0);

                                    if (name.Length == 0)
                                        continue;

                                    object_name = name.ToString();
                                }
                            }
                            catch
                            {
                            }

                            finally
                            {
                                duphandle.Dispose();
                            }

                            yield return new HandleTableEntryInformation(ref handle, object_type, object_name,
                                processInfo);
                        }
                    }
                }
            }
        }

        public static IEnumerable<int> EnumerateProcessesHoldingFileHandle(params string[] nativeFullPaths)
        {
            var paths = Array.ConvertAll(nativeFullPaths, path => new { path, dir_path = string.Concat(path, @"\") });
            /*
                 Return _
                     From handle In EnumerateHandleTableHandleInformation("File")
                     Where
                         Not String.IsNullOrWhiteSpace(handle.ObjectName) AndAlso
                         paths.Any(Function(path) handle.ObjectName.Equals(path.path, StringComparison.OrdinalIgnoreCase) OrElse
                             handle.ObjectName.StartsWith(path.dir_path, StringComparison.OrdinalIgnoreCase))
                     Select handle.HandleTableEntry.ProcessId
                     Distinct
     
      */
        }

        public static string FormatProcessName(int processId)
        {
            try
            {
                using (var ps = Process.GetProcessById(processId))
                {
                    if (ps.SessionId == 0 || string.IsNullOrWhiteSpace(ps.MainWindowTitle))
                        return $"'{ps.ProcessName}' (id={processId})";
                    else
                        return $"'{ps.MainWindowTitle}' (id={processId})";
                }
            }
            catch
            {
                return $"id={processId}";
            }
        }

        public static bool GetDiskFreeSpace(string lpRootPathName, out UInt32 lpSectorsPerCluster,
            out UInt32 lpBytesPerSector, out UInt32 lpNumberOfFreeClusters, ref UInt32 lpTotalNumberOfClusters)
        {
            return UnsafeNativeMethods.GetDiskFreeSpace(lpRootPathName, lpSectorsPerCluster, lpBytesPerSector, lpNumberOfFreeClusters, lpTotalNumberOfClusters);
        }


        /*
        public static bool DeviceIoControl(
            SafeFileHandle hDevice,
            UInt32 dwIoControlCode,
            <
    
        MarshalAs(UnmanagedType.LPArray),
        [in]> byte[] lpInBuffer,
            nInBufferSize As UInt32,
            < MarshalAs(UnmanagedType.LPArray, SizeParamIndex: = 6), Out > lpOutBuffer As Byte(),
        nOutBufferSize As UInt32,
            < Out > ByRef lpBytesReturned As UInt32,
            lpOverlapped As IntPtr) {
            Return UnsafeNativeMethods.DeviceIoControl(
                hDevice,
                dwIoControlCode,
                lpInBuffer,
                nInBufferSize,
                lpOutBuffer,
                nOutBufferSize,
                lpBytesReturned,
                lpOverlapped)
    
    
        }
        */


        public static bool DeviceIoControl(
            SafeFileHandle hDevice,
            UInt32 dwIoControlCode,
            IntPtr lpInBuffer,
            UInt32 nInBufferSize,
            IntPtr lpOutBuffer,
            UInt32 nOutBufferSize,
            ref UInt32 lpBytesReturned,
            IntPtr lpOverlapped)
        {
            return UnsafeNativeMethods.DeviceIoControl(
                hDevice,
                dwIoControlCode,
                lpInBuffer,
                nInBufferSize,
                lpOutBuffer,
                nOutBufferSize,
                lpBytesReturned,
                lpOverlapped);
        }

        public static bool SafeFileHandleDeviceIoControl(SafeFileHandlehDevice,
            UInt32 dwIoControlCode,
            SafeBuffer lpInBuffer,
            UInt32 nInBufferSize,
            SafeBuffer lpOutBuffer,
            UInt32 nOutBufferSize,
            ref UInt32 lpBytesReturned,
            IntPtr lpOverlapped)
        {
            if (nInBufferSize > lpInBuffer?.ByteLength)
            {
                throw new ArgumentException("Buffer size to use in call must be within size of SafeBuffer",
                    nameof(nInBufferSize));

            }

            if (nOutBufferSize > lpOutBuffer?.ByteLength)
            {
                throw new ArgumentException("Buffer size to use in call must be within size of SafeBuffer",
                    nameof(nOutBufferSize));

            }

            return UnsafeNativeMethods.DeviceIoControl(hDevice, dwIoControlCode, lpInBuffer, nInBufferSize,
                lpOutBuffer, nOutBufferSize, lpBytesReturned, lpOverlapped);

        }


        /// <summary>
        ///  Sends an IOCTL control request to a device driver, or an FSCTL control request to a filesystem driver.
        ///  </summary>
        ///  <param name="device">Open handle to filer or device.</param>
        ///  <param name="ctrlcode">IOCTL or FSCTL control code.</param>
        ///  <param name="data">Optional function to create input data for the control function.</param>
        ///  <param name="outdatasize">Number of bytes returned in output buffer by driver.</param>
        ///  <returns>This method returns a byte array that can be used to read and parse data returned by
        ///  driver in the output buffer.</returns>
        public static byte[] DeviceIoControl(SafeFileHandle device, UInt32 ctrlcode, byte[] data,
            ref UInt32 outdatasize)
        {
            var indatasize = data == null ? 0U : System.Convert.ToUInt32(data.Length);

            if (outdatasize > indatasize)
                Array.Resize(ref data, System.Convert.ToInt32(outdatasize));

            var rc = UnsafeNativeMethods.DeviceIoControl(device, ctrlcode, data, indatasize, data,
                data == null ? 0U : System.Convert.ToUInt32(data.Length), outdatasize, IntPtr.Zero);

            if (!rc)
                throw new Win32Exception();

            Array.Resize(ref data, System.Convert.ToInt32(outdatasize));

            return data;
        }

        public static FileSystemRights ConvertManagedFileAccess(FileAccess DesiredAccess)
        {
            var NativeDesiredAccess = FileSystemRights.ReadAttributes;

            if (DesiredAccess.HasFlag(FileAccess.Read))
                NativeDesiredAccess = NativeDesiredAccess | FileSystemRights.Read;
            if (DesiredAccess.HasFlag(FileAccess.Write))
                NativeDesiredAccess = NativeDesiredAccess | FileSystemRights.Write;

            return NativeDesiredAccess;
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="SecurityAttributes"></param>
        ///  <param name="FlagsAndAttributes"></param>
        ///  <param name="TemplateFile"></param>
        public static SafeFileHandle CreateFile(string FileName, FileSystemRights DesiredAccess, FileShare ShareMode,
            IntPtr SecurityAttributes, UInt32 CreationDisposition, Int32 FlagsAndAttributes, IntPtr TemplateFile)
        {
            var handle = UnsafeNativeMethods.CreateFile(FileName, DesiredAccess, ShareMode,
                SecurityAttributes,
                CreationDisposition, FlagsAndAttributes, TemplateFile);

            if (handle.IsInvalid)
                throw new IOException($"Cannot open '{FileName}'", new Win32Exception());

            return handle;
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode,
            FileMode CreationDisposition, bool Overlapped)
        {
            if (string.IsNullOrEmpty(FileName))
                throw new ArgumentNullException(nameof(FileName));

            var NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess);

            UInt32 NativeCreationDisposition;
            switch (CreationDisposition)
            {
                case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }

                case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }

                case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }

                case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }

                case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

                default:
                {
                    throw new NotImplementedException();
                }
            }

            var NativeFlagsAndAttributes = FileAttributes.Normal;
            if (Overlapped)
                NativeFlagsAndAttributes = NativeFlagsAndAttributes |
                                           (FileAttributes)NativeConstants.FILE_FLAG_OVERLAPPED;

            var Handle = UnsafeNativeMethods.CreateFile(FileName, NativeDesiredAccess, ShareMode,
                IntPtr.Zero,
                NativeCreationDisposition, NativeFlagsAndAttributes, IntPtr.Zero);

            if (Handle.IsInvalid)
                throw new IOException($"Cannot open {FileName}", new Win32Exception());

            return Handle;
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="Options">Specifies whether to request overlapped I/O.</param>
        public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode,
            FileMode CreationDisposition, FileOptions Options)
        {
            return OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition,
                System.Convert.ToUInt32(Options));
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="Options">Specifies whether to request overlapped I/O.</param>
        public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode,
            FileMode CreationDisposition, UInt32 Options)
        {
            if (string.IsNullOrEmpty(FileName))
                throw new ArgumentNullException(nameof(FileName));

            var NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess);

            UInt32 NativeCreationDisposition;
            switch (CreationDisposition)
            {
                case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }

                case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }

                case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }

                case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }

                case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

                default:
                {
                    throw new NotImplementedException();
                    break;
                }
            }

            var NativeFlagsAndAttributes = FileAttributes.Normal;

            NativeFlagsAndAttributes = NativeFlagsAndAttributes | (FileAttributes)Options;

            var Handle = UnsafeNativeMethods.CreateFile(FileName, NativeDesiredAccess, ShareMode,
                IntPtr.Zero,
                NativeCreationDisposition, NativeFlagsAndAttributes, IntPtr.Zero);
            if (Handle.IsInvalid)
                throw new IOException($"Cannot open {FileName}", new Win32Exception());

            return Handle;
        }

        /// <summary>
        ///  Calls NT API NtCreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationOption">Specifies whether to request overlapped I/O.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="FileAttributes">Attributes for created file.</param>
        ///  <param name="ObjectAttributes">Object attributes.</param>
        ///  <param name="RootDirectory">Root directory to start path parsing from, or null for rooted path.</param>
        ///  <param name="WasCreated">Return information about whether a file was created, existing file opened etc.</param>
        ///  <returns>NTSTATUS value indicating result of the operation.</returns>
        public static SafeFileHandle NtCreateFile(string FileName, NtObjectAttributes ObjectAttributes,
            FileAccess DesiredAccess, FileShare ShareMode, NtCreateDisposition CreationDisposition,
            NtCreateOptions CreationOption, FileAttributes FileAttributes, SafeFileHandle RootDirectory,
            out NtFileCreated WasCreated)
        {
            if (string.IsNullOrEmpty(FileName))
                throw new ArgumentNullException(nameof(FileName));

            var native_desired_access = ConvertManagedFileAccess(DesiredAccess) | FileSystemRights.Synchronize;

            SafeFileHandle handle_value = null;

            using (PinnedString pinned_name_string = new PinnedString(FileName))
            using (var unicode_string_name = PinnedBuffer.Serialize(pinned_name_string.UnicodeString)
                  )
            {
                ObjectAttributes object_attributes = new ObjectAttributes(
                    RootDirectory?.DangerousGetHandle() ?? IntPtr.Zero, unicode_string_name.DangerousGetHandle(),
                    ObjectAttributes, null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */);

                IoStatusBlock io_status_block;

                var status = UnsafeNativeMethods.NtCreateFile(handle_value, native_desired_access,
                    object_attributes, io_status_block, 0, FileAttributes, ShareMode, CreationDisposition,
                    CreationOption,
                    null /* TODO Change to default(_) if this is not a reference type */, 0);

                WasCreated = (NtFileCreated)io_status_block.Information;

                if (status < 0)
                    throw GetExceptionForNtStatus(status);
            }

            return handle_value;
        }

        /// <summary>
        ///  Calls NT API NtOpenEvent() function to open an event object using NT path and encapsulates returned handle in a SafeWaitHandle object.
        ///  </summary>
        ///  <param name="EventName">Name of event to open.</param>
        ///  <param name="DesiredAccess">Access to request.</param>
        ///  <param name="ObjectAttributes">Object attributes.</param>
        ///  <param name="RootDirectory">Root directory to start path parsing from, or null for rooted path.</param>
        ///  <returns>NTSTATUS value indicating result of the operation.</returns>
        public static SafeWaitHandle NtOpenEvent(string EventName, NtObjectAttributes ObjectAttributes,
            UInt32 DesiredAccess, SafeFileHandle RootDirectory)
        {
            if (string.IsNullOrEmpty(EventName))
                throw new ArgumentNullException(nameof(EventName));

            SafeWaitHandle handle_value = null;

            using (PinnedString pinned_name_string = new PinnedString(EventName))
            using (var unicode_string_name = PinnedBuffer.Serialize(pinned_name_string.UnicodeString)
                  )
            {
                ObjectAttributes object_attributes = new ObjectAttributes(
                    RootDirectory?.DangerousGetHandle() ?? IntPtr.Zero, unicode_string_name.DangerousGetHandle(),
                    ObjectAttributes, null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */);

                var status =
                    UnsafeNativeMethods.NtOpenEvent(handle_value, DesiredAccess, object_attributes);

                if (status < 0)
                    throw GetExceptionForNtStatus(status);
            }

            return handle_value;
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function to create a backup handle for a file or
        ///  directory and encapsulates returned handle in a SafeFileHandle object. This
        ///  handle can later be used in calls to Win32 Backup API functions or similar.
        ///  </summary>
        ///  <param name="FilePath">Name of file or directory to open.</param>
        ///  <param name="DesiredAccess">Access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        public static SafeFileHandle OpenBackupHandle(string FilePath, FileAccess DesiredAccess, FileShare ShareMode,
            FileMode CreationDisposition)
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new ArgumentNullException(nameof(FilePath));

            var NativeDesiredAccess = FileSystemRights.ReadAttributes;
            if (DesiredAccess.HasFlag(FileAccess.Read))
                NativeDesiredAccess = NativeDesiredAccess | FileSystemRights.Read;
            if (DesiredAccess.HasFlag(FileAccess.Write))
                NativeDesiredAccess = NativeDesiredAccess | FileSystemRights.Write;

            UInt32 NativeCreationDisposition;
            switch (CreationDisposition)
            {
                case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }

                case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }

                case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }

                case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }

                case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

                default:
                {
                    throw new NotImplementedException();
                }
            }

            var NativeFlagsAndAttributes = (FileAttributes)NativeConstants.FILE_FLAG_BACKUP_SEMANTICS;

            var Handle = UnsafeNativeMethods.CreateFile(FilePath, NativeDesiredAccess, ShareMode,
                IntPtr.Zero,
                NativeCreationDisposition, NativeFlagsAndAttributes, IntPtr.Zero);

            if (Handle.IsInvalid)
                throw new IOException($"Cannot open {FilePath}", new Win32Exception());

            return Handle;
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function to create a backup handle for a file or
        ///  directory and encapsulates returned handle in a SafeFileHandle object. This
        ///  handle can later be used in calls to Win32 Backup API functions or similar.
        ///  </summary>
        ///  <param name="FilePath">Name of file or directory to open.</param>
        ///  <param name="DesiredAccess">Access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        public static SafeFileHandle TryOpenBackupHandle(string FilePath, FileAccess DesiredAccess,
            FileShare ShareMode, FileMode CreationDisposition)
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new ArgumentNullException(nameof(FilePath));

            var NativeDesiredAccess = FileSystemRights.ReadAttributes;
            if (DesiredAccess.HasFlag(FileAccess.Read))
                NativeDesiredAccess = NativeDesiredAccess | FileSystemRights.Read;
            if (DesiredAccess.HasFlag(FileAccess.Write))
                NativeDesiredAccess = NativeDesiredAccess | FileSystemRights.Write;

            UInt32 NativeCreationDisposition;
            switch (CreationDisposition)
            {
                case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }

                case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }

                case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }

                case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }

                case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

                default:
                {
                    throw new NotImplementedException();
                    break;
                }
            }

            var NativeFlagsAndAttributes = (FileAttributes)NativeConstants.FILE_FLAG_BACKUP_SEMANTICS;

            var Handle = UnsafeNativeMethods.CreateFile(FilePath, NativeDesiredAccess, ShareMode,
                IntPtr.Zero,
                NativeCreationDisposition, NativeFlagsAndAttributes, IntPtr.Zero);

            if (Handle.IsInvalid)
                Trace.WriteLine($"Cannot open {FilePath} ({Marshal.GetLastWin32Error()})");

            return Handle;
        }

        /// <summary>
        ///  Converts FileAccess flags to values legal in constructor call to FileStream class.
        ///  </summary>
        ///  <param name="Value">FileAccess values.</param>
        private static FileAccess GetFileStreamLegalAccessValue(FileAccess Value)
        {
            if (Value == 0)
                return FileAccess.Read;
            else
                return Value;
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition,
            FileAccess DesiredAccess, FileShare ShareMode)
        {

            /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
            /* TODO ERROR: Skipped ElseDirectiveTrivia */
            return new FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode, bufferSize: 1,
                useAsync: true);
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="BufferSize">Buffer size to specify in constructor call to FileStream class.</param>
        public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition,
            FileAccess DesiredAccess, FileShare ShareMode, int BufferSize)
        {

            /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
            /* TODO ERROR: Skipped ElseDirectiveTrivia */
            return new FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode, BufferSize,
                FileOptions.Asynchronous);
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="BufferSize">Buffer size to specify in constructor call to FileStream class.</param>
        ///  <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition,
            FileAccess DesiredAccess, FileShare ShareMode, int BufferSize, bool Overlapped)
        {
            return new FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped),
                GetFileStreamLegalAccessValue(DesiredAccess), BufferSize, Overlapped);
        }




        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition,
            FileAccess DesiredAccess, FileShare ShareMode, bool Overlapped)
        {
            return new FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped),
                GetFileStreamLegalAccessValue(DesiredAccess), 1, Overlapped);
        }

        /// <summary>
        ///  Calls Win32 API CreateFile() function and encapsulates returned handle.
        ///  </summary>
        ///  <param name="FileName">Name of file to open.</param>
        ///  <param name="DesiredAccess">File access to request.</param>
        ///  <param name="ShareMode">Share mode to request.</param>
        ///  <param name="CreationDisposition">Open/creation mode.</param>
        ///  <param name="Options">Specifies whether to request overlapped I/O.</param>
        public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition,
            FileAccess DesiredAccess, FileShare ShareMode, FileOptions Options)
        {
            return new FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Options),
                GetFileStreamLegalAccessValue(DesiredAccess), 1, Options.HasFlag(FileOptions.Asynchronous));
        }

        private static void SetFileCompressionState(SafeFileHandle SafeFileHandle, ushort State)
        {
            var pinptr = GCHandle.Alloc(State, GCHandleType.Pinned);
            try
            {
                Win32Try(UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                    NativeConstants.FSCTL_SET_COMPRESSION, pinptr.AddrOfPinnedObject(), 2U, IntPtr.Zero, 0U,
                    null /* TODO Change to default(_) if this is not a reference type */, IntPtr.Zero));
            }
            finally
            {
                pinptr.Free();
            }
        }

        public static Int64 GetFileSize(string Filename)
        {
            using (var safefilehandle =
                   TryOpenBackupHandle(Filename, 0, FileShare.ReadWrite | FileShare.Delete, FileMode.Open))
            {
                if (safefilehandle.IsInvalid)
                    return -1;

                return GetFileSize(safefilehandle);
            }
        }

        public static Int64 GetFileSize(SafeFileHandle SafeFileHandle)
        {
            Int64 FileSize;

            Win32Try(UnsafeNativeMethods.GetFileSizeEx(SafeFileHandle, FileSize));

            return FileSize;
        }

        public static Int64? GetDiskSize(SafeFileHandle SafeFileHandle)
        {
            Int64 FileSize;

            if (UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                    NativeConstants.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0U, FileSize,
                    System.Convert.ToUInt32(Marshal.SizeOf(FileSize)), 0U, IntPtr.Zero))
                return FileSize;
            else
                return default(Long?);
        }

        public static FILE_FS_FULL_SIZE_INFORMATION? GetVolumeSizeInformation(
            SafeFileHandle SafeFileHandle)
        {
            using (PinnedBuffer<FILE_FS_FULL_SIZE_INFORMATION> buffer =
                   new PinnedBuffer<FILE_FS_FULL_SIZE_INFORMATION>(1))
            {
                IoStatusBlock io_status_block = new IoStatusBlock();

                var status = UnsafeNativeMethods.NtQueryVolumeInformationFile(SafeFileHandle,
                    io_status_block,
                    buffer, System.Convert.ToInt32(buffer.ByteLength),
                    FsInformationClass.FileFsFullSizeInformation);

                if (status < 0)
                    return default(FILE_FS_FULL_SIZE_INFORMATION?);

                return buffer.Read<FILE_FS_FULL_SIZE_INFORMATION>(0);
            }
        }

        public static bool IsDiskWritable(SafeFileHandle SafeFileHandle)
        {
            var rc = UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                NativeConstants.IOCTL_DISK_IS_WRITABLE, IntPtr.Zero, 0U, IntPtr.Zero, 0U, 0U, IntPtr.Zero);
            if (rc)
                return true;
            else
            {
                var err = Marshal.GetLastWin32Error();

                switch (err)
                {
                    case object _ when NativeConstants.ERROR_WRITE_PROTECT:
                    case object _ when NativeConstants.ERROR_NOT_READY:
                    case object _ when NativeConstants.FVE_E_LOCKED_VOLUME:
                    {
                        return false;
                    }

                    default:
                    {
                        throw new Win32Exception(err);
                        break;
                    }
                }
            }
        }

        public static void GrowPartition(SafeFileHandle DiskHandle, int PartitionNumber, Int64 BytesToGrow)
        {
            DISK_GROW_PARTITION DiskGrowPartition;
            DiskGrowPartition.PartitionNumber = PartitionNumber;
            DiskGrowPartition.BytesToGrow = BytesToGrow;
            Win32Try(UnsafeNativeMethods.DeviceIoControl(DiskHandle,
                NativeConstants.IOCTL_DISK_GROW_PARTITION, DiskGrowPartition,
                System.Convert.ToUInt32(PinnedBuffer<DISK_GROW_PARTITION>.TypeSize), IntPtr.Zero, 0U, 0U,
                IntPtr.Zero));
        }

        public static void CompressFile(SafeFileHandle SafeFileHandle)
        {
            SetFileCompressionState(SafeFileHandle, NativeConstants.COMPRESSION_FORMAT_DEFAULT);
        }

        public static void UncompressFile(SafeFileHandle SafeFileHandle)
        {
            SetFileCompressionState(SafeFileHandle, NativeConstants.COMPRESSION_FORMAT_NONE);
        }

        public static void AllowExtendedDASDIO(SafeFileHandle SafeFileHandle)
        {
            Win32Try(UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                NativeConstants.FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0U, IntPtr.Zero, 0U, 0U,
                IntPtr.Zero));
        }

        public static string GetLongFullPath(string path)
        {
            path = GetNtPath(path);

            if (path.StartsWith(@"\??\", StringComparison.Ordinal))
                path = $@"\\?\{path.Substring(4)}";

            return path;
        }

        /// <summary>
        ///  Adds a semicolon separated list of paths to the PATH environment variable of
        ///  current process. Any paths already in present PATH variable are not added again.
        ///  </summary>
        ///  <param name="AddPaths">Semicolon separated list of directory paths</param>
        ///  <param name="BeforeExisting">Indicates whether to insert new paths before existing path list or move
        ///  existing of specified paths first if True, or add new paths after existing path list if False.</param>
        public static void AddProcessPaths(bool BeforeExisting, string AddPaths)
        {
            if (string.IsNullOrEmpty(AddPaths))
                return;

            var AddPathsArray = AddPaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            AddProcessPaths(BeforeExisting, AddPathsArray);
        }

        /// <summary>
        ///  Adds a list of paths to the PATH environment variable of current process. Any
        ///  paths already in present PATH variable are not added again.
        ///  </summary>
        ///  <param name="AddPathsArray">Array of directory paths</param>
        ///  <param name="BeforeExisting">Indicates whether to insert new paths before existing path list or move
        ///  existing of specified paths first if True, or add new paths after existing path list if False.</param>
        public static void AddProcessPaths(bool BeforeExisting, params string[] AddPathsArray)
        {
            if (AddPathsArray == null || AddPathsArray.Length == 0)
                return;

            List<string> Paths = new List<string>(Environment.GetEnvironmentVariable("PATH")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            if (BeforeExisting)
            {
                foreach (var AddPath in AddPathsArray)
                {
                    if (Paths.BinarySearch(AddPath, StringComparer.CurrentCultureIgnoreCase) >= 0)
                        Paths.Remove(AddPath);
                }

                Paths.InsertRange(0, AddPathsArray);
            }
            else
                foreach (var AddPath in AddPathsArray)
                {
                    if (Paths.BinarySearch(AddPath, StringComparer.CurrentCultureIgnoreCase) < 0)
                        Paths.Add(AddPath);
                }

            Environment.SetEnvironmentVariable("PATH", string.Join(";", Paths.ToArray()));
        }

        /// <summary>
        ///  Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
        ///  can only be done through the handle passed to this function until handle is closed or lock is
        ///  released.
        ///  </summary>
        ///  <param name="Device">Handle to device to lock and dismount.</param>
        ///  <param name="Force">Indicates if True that volume should be immediately dismounted even if it
        ///  cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
        ///  successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
        public static bool DismountVolumeFilesystem(SafeFileHandle Device, bool Force)
        {
            bool lock_result;

            for (var i = 0; i <= 10; i++)
            {
                if (i > 0)
                    Trace.WriteLine("Error locking volume, retrying...");

                UnsafeNativeMethods.FlushFileBuffers(Device);

                Thread.Sleep(300);

                lock_result = UnsafeNativeMethods.DeviceIoControl(Device,
                    NativeConstants.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0,
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */);
                if (lock_result || Marshal.GetLastWin32Error() != NativeConstants.ERROR_ACCESS_DENIED)
                    break;
            }

            if (!lock_result && !Force)
                return false;

            return UnsafeNativeMethods.DeviceIoControl(Device,
                NativeConstants.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0,
                null /* TODO Change to default(_) if this is not a reference type */,
                null /* TODO Change to default(_) if this is not a reference type */);
        }


        /// <summary>
        ///  Retrieves disk geometry.
        ///  </summary>
        ///  <param name="hDevice">Handle to device.</param>
        public static DISK_GEOMETRY? GetDiskGeometry(SafeFileHandle hDevice)
        {
            DISK_GEOMETRY DiskGeometry;

            if (UnsafeNativeMethods.DeviceIoControl(hDevice,
                    NativeConstants.IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, DiskGeometry,
                    System.Convert.ToUInt32(PinnedBuffer<DISK_GEOMETRY>.TypeSize),
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */))
                return DiskGeometry;
            else
                return default(DISK_GEOMETRY?);
        }

        /// <summary>
        ///  Retrieves SCSI address.
        ///  </summary>
        ///  <param name="hDevice">Handle to device.</param>
        public static SCSI_ADDRESS? GetScsiAddress(SafeFileHandle hDevice)
        {
            SCSI_ADDRESS ScsiAddress;

            if (UnsafeNativeMethods.DeviceIoControl(hDevice,
                    NativeConstants.IOCTL_SCSI_GET_ADDRESS, IntPtr.Zero, 0, ScsiAddress,
                    System.Convert.ToUInt32(PinnedBuffer<SCSI_ADDRESS>.TypeSize),
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */))
                return ScsiAddress;
            else
                return default(SCSI_ADDRESS?);
        }

        /// <summary>
        ///  Retrieves SCSI address.
        ///  </summary>
        ///  <param name="Device">Path to device.</param>
        public static SCSI_ADDRESS? GetScsiAddress(string Device)
        {
            using (var hDevice = OpenFileHandle(Device, 0, FileShare.ReadWrite, FileMode.Open, false))
            {
                return GetScsiAddress(hDevice);
            }
        }

        /// <summary>
        ///  Retrieves status of write overlay for mounted device.
        ///  </summary>
        ///  <param name="NtDevicePath">Path to device.</param>
        public static SCSI_ADDRESS? GetScsiAddressForNtDevice(string NtDevicePath)
        {
            try
            {
                using (var hDevice = NtCreateFile(NtDevicePath, 0, 0, FileShare.ReadWrite,
                           NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0,
                           null /* TODO Change to default(_) if this is not a reference type */,
                           null /* TODO Change to default(_) if this is not a reference type */))
                {
                    return GetScsiAddress(hDevice);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error getting SCSI address for device '{NtDevicePath}': {ex.JoinMessages()}");
                return default(SCSI_ADDRESS?);
            }
        }

        /// <summary>
        ///  Retrieves storage standard properties.
        ///  </summary>
        ///  <param name="hDevice">Handle to device.</param>
        public static StorageStandardProperties? GetStorageStandardProperties(SafeFileHandle hDevice)
        {
            STORAGE_PROPERTY_QUERY StoragePropertyQuery =
                new STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID.StorageDeviceProperty,
                    STORAGE_QUERY_TYPE.PropertyStandardQuery);
            STORAGE_DESCRIPTOR_HEADER StorageDescriptorHeader = new STORAGE_DESCRIPTOR_HEADER();

            if (!UnsafeNativeMethods.DeviceIoControl(hDevice,
                    NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY, StoragePropertyQuery,
                    System.Convert.ToUInt32(PinnedBuffer<STORAGE_PROPERTY_QUERY>.TypeSize),
                    StorageDescriptorHeader,
                    System.Convert.ToUInt32(PinnedBuffer<STORAGE_DESCRIPTOR_HEADER>.TypeSize),
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */))
                return default(StorageStandardProperties?);
            ; /* 
           * 
            Using buffer As New PinnedBuffer(Of Byte)(New Byte(0 To CInt(StorageDescriptorHeader.Size - 1)) {})

                If Not UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                   StoragePropertyQuery, CUInt(PinnedBuffer(Of STORAGE_PROPERTY_QUERY).TypeSize),
                                                   buffer, CUInt(buffer.ByteLength),
                                                   Nothing, Nothing) Then
                    Return Nothing
                End If

                Return New StorageStandardProperties(buffer)

            End Using

        */
        }

        /// <summary>
        ///  Retrieves storage TRIM properties.
        ///  </summary>
        ///  <param name="hDevice">Handle to device.</param>
        public static bool? GetStorageTrimProperties(SafeFileHandle hDevice)
        {
            STORAGE_PROPERTY_QUERY StoragePropertyQuery =
                new STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID.StorageDeviceTrimProperty,
                    STORAGE_QUERY_TYPE.PropertyStandardQuery);
            DEVICE_TRIM_DESCRIPTOR DeviceTrimDescriptor = new DEVICE_TRIM_DESCRIPTOR();

            if (!UnsafeNativeMethods.DeviceIoControl(hDevice,
                    NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY, StoragePropertyQuery,
                    System.Convert.ToUInt32(PinnedBuffer<STORAGE_PROPERTY_QUERY>.TypeSize),
                    DeviceTrimDescriptor,
                    System.Convert.ToUInt32(PinnedBuffer<DEVICE_TRIM_DESCRIPTOR>.TypeSize),
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */))
                return default(Boolean?);

            return DeviceTrimDescriptor.TrimEnabled != 0;
        }

        /// <summary>
        ///  Retrieves storage device number.
        ///  </summary>
        ///  <param name="hDevice">Handle to device.</param>
        public static STORAGE_DEVICE_NUMBER? GetStorageDeviceNumber(SafeFileHandle hDevice)
        {
            STORAGE_DEVICE_NUMBER StorageDeviceNumber;

            if (UnsafeNativeMethods.DeviceIoControl(hDevice,
                    NativeConstants.IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, StorageDeviceNumber,
                    System.Convert.ToUInt32(PinnedBuffer<STORAGE_DEVICE_NUMBER>.TypeSize),
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */))
                return StorageDeviceNumber;
            else
                return default(STORAGE_DEVICE_NUMBER?);
        }

        /// <summary>
        ///  Retrieves PhysicalDrive or CdRom path for NT raw device path
        ///  </summary>
        ///  <param name="ntdevice">NT device path, such as \Device\00000001.</param>
        public static string GetPhysicalDriveNameForNtDevice(string ntdevice)
        {
            using (var hDevice = NtCreateFile(ntdevice, 0, 0, FileShare.ReadWrite, NtCreateDisposition.Open, 0,
                       0, null /* TODO Change to default(_) if this is not a reference type */,
                       null /* TODO Change to default(_) if this is not a reference type */))
            {
                var devnr = GetStorageDeviceNumber(hDevice);

                if (!devnr.HasValue || devnr.Value.PartitionNumber > 0)
                    throw new InvalidOperationException($"Device '{ntdevice}' is not a physical disk device object");

                switch (devnr.Value.DeviceType)
                {
                    case DeviceType.CdRom:
                    {
                        return $"CdRom{devnr.Value.DeviceNumber}";
                    }

                    case DeviceType.Disk:
                    {
                        return $"PhysicalDrive{devnr.Value.DeviceNumber}";
                    }

                    default:
                    {
                        throw new InvalidOperationException(
                            $"Device '{ntdevice}' has unknown device type 0x{System.Convert.ToInt32(devnr.Value.DeviceType)}");
                    }
                }
            }
        }

        /// <summary>
        ///  Returns directory junction target path
        ///  </summary>
        ///  <param name="source">Location of directory that is a junction.</param>
        public static string QueryDirectoryJunction(string source)
        {
            using (var hdir = OpenFileHandle(source, FileAccess.Write, FileShare.Read, FileMode.Open,
                       NativeConstants.FILE_FLAG_BACKUP_SEMANTICS |
                       NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT))
            {
                return QueryDirectoryJunction(hdir);
            }
        }

        /// <summary>
        ///  Creates a directory junction
        ///  </summary>
        ///  <param name="source">Location of directory to convert to a junction.</param>
        ///  <param name="target">Target path for the junction.</param>
        public static void CreateDirectoryJunction(string source, string target)
        {
            Directory.CreateDirectory(source);

            using (var hdir = OpenFileHandle(source, FileAccess.Write, FileShare.Read, FileMode.Open,
                       NativeConstants.FILE_FLAG_BACKUP_SEMANTICS |
                       NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT))
            {
                CreateDirectoryJunction(hdir, target);
            }
        }

        public static void SetFileSparseFlag(SafeFileHandle file, bool flag)
        {
            Win32Try(UnsafeNativeMethods.DeviceIoControl(file, NativeConstants.FSCTL_SET_SPARSE,
                flag, 1, null /* TODO Change to default(_) if this is not a reference type */, 0,
                null /* TODO Change to default(_) if this is not a reference type */,
                null /* TODO Change to default(_) if this is not a reference type */));
        }

        /// <summary>
        ///  Get directory junction target path
        ///  </summary>
        ///  <param name="source">Handle to directory.</param>
        public static string QueryDirectoryJunction(SafeFileHandle source)
        {

            var buffer = new byte[65533];

            uint size;

            if (!UnsafeNativeMethods.DeviceIoControl(source,
                    NativeConstants.FSCTL_GET_REPARSE_POINT, IntPtr.Zero, 0U, buffer,
                    System.Convert.ToUInt32(buffer.Length), size, IntPtr.Zero))
                return null;

            using (BinaryReader wr = new BinaryReader(new MemoryStream(buffer, 0, System.Convert.ToInt32(size))))
            {
                if (wr.ReadUInt32() != NativeConstants.IO_REPARSE_TAG_MOUNT_POINT)
                    throw new InvalidDataException("Not a mount point or junction"); // DWORD ReparseTag
                wr.ReadUInt16(); // WORD ReparseDataLength
                wr.ReadUInt16(); // WORD Reserved
                wr.ReadUInt16(); // WORD NameOffset
                var name_length = wr.ReadUInt16() - 2; // WORD NameLength
                wr.ReadUInt16(); // WORD DisplayNameOffset
                wr.ReadUInt16(); // WORD DisplayNameLength
                return Encoding.Unicode.GetString(wr.ReadBytes(name_length));
            }
        }

        /// <summary>
        ///  Creates a directory junction
        ///  </summary>
        ///  <param name="source">Handle to directory.</param>
        ///  <param name="target">Target path for the junction.</param>
        public static void CreateDirectoryJunction(SafeFileHandle source, string target)
        {
            var name = Encoding.Unicode.GetBytes(target);

            using (BufferedBinaryWriter wr = new BufferedBinaryWriter())
            {
                wr.Write(NativeConstants.IO_REPARSE_TAG_MOUNT_POINT); // DWORD ReparseTag
                wr.Write(8 + System.Convert.ToInt16(name.Length) + 2 + System.Convert.ToInt16(name.Length) +
                         2); // WORD ReparseDataLength
                wr.Write(0); // WORD Reserved
                wr.Write(0); // WORD NameOffset
                wr.Write(System.Convert.ToInt16(name.Length)); // WORD NameLength
                wr.Write(System.Convert.ToInt16(name.Length) + 2); // WORD DisplayNameOffset
                wr.Write(System.Convert.ToInt16(name.Length)); // WORD DisplayNameLength
                wr.Write(name);
                wr.Write(new char());
                wr.Write(name);
                wr.Write(new char());

                var buffer = wr.ToArray();

                if (!UnsafeNativeMethods.DeviceIoControl(source,
                        NativeConstants.FSCTL_SET_REPARSE_POINT, buffer,
                        System.Convert.ToUInt32(buffer.Length), IntPtr.Zero, 0U, 0U, IntPtr.Zero))
                    throw new Win32Exception();
            }
        }


        public static IEnumerable<string> QueryDosDevice()
        {
            return QueryDosDevice(null);
        }

        public static IEnumerable<string> QueryDosDevice(string DosDevice)
        {

            var TargetPath = new char[65536];

            var length = UnsafeNativeMethods.QueryDosDevice(DosDevice, TargetPath, TargetPath.Length);

            if (length < 2)
                return null;

            return ParseDoubleTerminatedString(TargetPath, length);
        }

        public static string GetNtPath(string Win32Path)
        {
            UNICODE_STRING unicode_string;

            var RC = UnsafeNativeMethods.RtlDosPathNameToNtPathName_U(Win32Path, unicode_string,
                null /* TODO Change to default(_) if this is not a reference type */,
                null /* TODO Change to default(_) if this is not a reference type */);
            if (!RC)
                throw new IOException($"Invalid path: '{Win32Path}'");

            try
            {
                return unicode_string.ToString();
            }
            finally
            {
                UnsafeNativeMethods.RtlFreeUnicodeString(unicode_string);
            }
        }

        public static void DeleteVolumeMountPoint(string VolumeMountPoint)
        {
            Win32Try(UnsafeNativeMethods.DeleteVolumeMountPoint(VolumeMountPoint));
        }

        public static void SetVolumeMountPoint(string VolumeMountPoint, string VolumeName)
        {
            Win32Try(UnsafeNativeMethods.SetVolumeMountPoint(VolumeMountPoint, VolumeName));
        }

        public static char FindFirstFreeDriveLetter()
        {
            return FindFirstFreeDriveLetter('D');
        }

        public static char FindFirstFreeDriveLetter(char start)
        {
            start = char.ToUpperInvariant(start);
            if (start < 'A' || start > 'Z')
                throw new ArgumentOutOfRangeException(nameof(start));

            var logical_drives = SafeNativeMethods.GetLogicalDrives();

            for (var search = Convert.ToUInt16(start); search <= Convert.ToUInt16('Z'); search++)
            {
                if ((logical_drives & (1 << (search - Convert.ToUInt16('A')))) == 0)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey($@"Network\{search}"))
                    {
                        if (key == null)
                            return Convert.ToChar(search);
                    }
                }
            }

            return default(Char);
        }

        public static Version GetFileVersion(Stream exe)
        {
            MemoryStream buffer = new MemoryStream();
            exe.CopyTo(buffer);

            return GetFileVersion(buffer.GetBuffer());
        }

        public static Version GetFileVersion(string exepath)
        {
            byte[] buffer;

            using (var exe = OpenFileStream(exepath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {

                var buffer = new byte[exe.Length];

                exe.Read(buffer, 0, buffer.Length);
            }

            return GetFileVersion(buffer);
        }

        public static Version GetFileVersion(byte[] exe)
        {
            var filever = NativeFileVersion.GetNativeFileVersion(exe);

            return filever.FileVersion;
        }

        public static string ReadNullTerminatedString(byte[] buffer, ref int offset)
        {
            var ptr = MemoryMarshal.Cast<byte, char>(buffer);

            offset = ptr.IndexOf(new char());

            if (offset < 0)
                offset = buffer.Length;

            return ptr.Slice(0, offset).ToString();
        }

        public static IMAGE_NT_HEADERS GetExeFileHeader(Stream exe)
        {

            var buffer = new byte[exe.Length];

            exe.Read(buffer, 0, buffer.Length);

            return GetExeFileHeader(buffer);
        }

        public static IMAGE_NT_HEADERS GetExeFileHeader(string exepath)
        {
            using (var exe = OpenFileStream(exepath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                return GetExeFileHeader(exe);
            }
        }

        public static IMAGE_NT_HEADERS GetExeFileHeader(byte[] exe)
        {
            return NativePE.GetImageNtHeaders(exe);
        }

        public static int PadValue(int value, int align)
        {
            return (value + align - 1) & -align;
        }

        public static DiskExtent[] GetVolumeDiskExtents(SafeFileHandle volume)
        {

            // 776 is enough to hold 32 disk extent items
            using (var buffer = PinnedBuffer.Create(DeviceIoControl(volume,
                       NativeConstants.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                       null /* TODO Change to default(_) if this is not a reference type */, 776)))
            {
                var number = buffer.Read<int>(0);

                var array = DiskExtent[number];


                buffer.ReadArray(8, array, 0, number);

                return array;
            }
        }

        public static PARTITION_INFORMATION? GetPartitionInformation(string DevicePath)
        {
            using (var devicehandle =
                   OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, 0))
            {
                return GetPartitionInformation(devicehandle);
            }
        }

        public static PARTITION_INFORMATION? GetPartitionInformation(SafeFileHandle disk)
        {
            PARTITION_INFORMATION partition_info = null;

            if (UnsafeNativeMethods.DeviceIoControl(disk,
                    NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX, IntPtr.Zero, 0, partition_info,
                    System.Convert.ToUInt32(Marshal.SizeOf(partition_info)), 0, IntPtr.Zero))
                return partition_info;
            else
                return default(PARTITION_INFORMATION?);
        }

        public static PARTITION_INFORMATION_EX? GetPartitionInformationEx(string DevicePath)
        {
            using (var devicehandle =
                   OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, 0))
            {
                return GetPartitionInformationEx(devicehandle);
            }
        }

        public static PARTITION_INFORMATION_EX? GetPartitionInformationEx(SafeFileHandle disk)
        {
            PARTITION_INFORMATION_EX partition_info;

            if (UnsafeNativeMethods.DeviceIoControl(disk,
                    NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX, IntPtr.Zero, 0, partition_info,
                    System.Convert.ToUInt32(Marshal.SizeOf(partition_info)), 0, IntPtr.Zero))
                return partition_info;
            else
                return default(PARTITION_INFORMATION_EX?);
        }



        public static DriveLayoutInformation GetDriveLayoutEx(string DevicePath)
        {
            using (var devicehandle =
                   OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, 0))
            {
                return GetDriveLayoutEx(devicehandle);
            }
        }

        public static DriveLayoutInformation GetDriveLayoutEx(SafeFileHandle disk)
        {
            var max_partitions = 1;

            do
            {
                var size_needed = PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize +
                                  PinnedBuffer<DRIVE_LAYOUT_INFORMATION_GPT>.TypeSize + max_partitions *
                                  PinnedBuffer<PARTITION_INFORMATION_EX>.TypeSize;
                using (PinnedBuffer<byte> buffer = new PinnedBuffer<byte>(size_needed))
                {
                    if (!UnsafeNativeMethods.DeviceIoControl(disk,
                            NativeConstants.IOCTL_DISK_GET_DRIVE_LAYOUT_EX, IntPtr.Zero, 0, buffer,
                            System.Convert.ToUInt32(buffer.ByteLength), 0, IntPtr.Zero))
                    {
                        if (Marshal.GetLastWin32Error() == NativeConstants.ERROR_INSUFFICIENT_BUFFER)
                        {
                            max_partitions *= 2;
                            continue;
                        }

                        return null ;
                    }

                    var layout = buffer.Read<DRIVE_LAYOUT_INFORMATION_EX>(0);
                    if (layout.PartitionCount > max_partitions)
                    {
                        max_partitions *= 2;
                        continue;
                    }


                    var partitions = PARTITION_INFORMATION_EX[layout.PartitionCount];

                    for (var i = 0; i <= layout.PartitionCount - 1; i++)
                        partitions[i] = (PARTITION_INFORMATION_EX)Marshal.PtrToStructure(
                            buffer.DangerousGetHandle() + 48 +
                            i * PinnedBuffer<PARTITION_INFORMATION_EX>.TypeSize,
                            typeof(PARTITION_INFORMATION_EX));
                    if (layout.PartitionStyle == PARTITION_STYLE.MBR)
                    {
                        var mbr = buffer.Read<DRIVE_LAYOUT_INFORMATION_MBR>(8);
                        return new DriveLayoutInformationMBR(layout, partitions, mbr);
                    }
                    else if (layout.PartitionStyle == PARTITION_STYLE.GPT)
                    {
                        var gpt = buffer.Read<DRIVE_LAYOUT_INFORMATION_GPT>(8);
                        return new DriveLayoutInformationGPT(layout, partitions, gpt);
                    }
                    else
                        return new DriveLayoutInformation(layout, partitions);
                }
            } while (true);
        }

        public static void SetDriveLayoutEx(SafeFileHandle disk, DriveLayoutInformation layout)
        {

            var partition_struct_size = PinnedBuffer( PARTITION_INFORMATION_EX).TypeSize;
            var drive_layout_information_ex_size = PinnedBuffer(DRIVE_LAYOUT_INFORMATION_EX).TypeSize;
            var drive_layout_information_record_size = PinnedBuffer(DRIVE_LAYOUT_INFORMATION_GPT).TypeSize;

            layout.NullCheck(nameof(layout));

            var partition_count = Math.Min(layout.Partitions.Count, layout.DriveLayoutInformation.PartitionCount);

            var size_needed = drive_layout_information_ex_size + drive_layout_information_record_size +
                              partition_count * partition_struct_size;

            var pos = 0;

            using (PinnedBuffer<byte> buffer = new PinnedBuffer<byte>(size_needed))
            {
                buffer.Write(System.Convert.ToUInt64(pos), layout.DriveLayoutInformation);

                pos += drive_layout_information_ex_size;

                switch (layout.DriveLayoutInformation.PartitionStyle)
                {
                    case PARTITION_STYLE.MBR:
                    {
                        buffer.Write(System.Convert.ToUInt64(pos), (DriveLayoutInformationMBR)layout.MBR);
                        break;
                    }

                    case PARTITION_STYLE.GPT:
                    {
                        buffer.Write(System.Convert.ToUInt64(pos), (DriveLayoutInformationGPT)layout.GPT);
                        break;
                    }
                }

                pos += drive_layout_information_record_size;

                for (var i = 0; i <= partition_count - 1; i++)
                    Marshal.StructureToPtr(layout.Partitions(i),
                        buffer.DangerousGetHandle() + pos + i * partition_struct_size, false);

                var rc = UnsafeNativeMethods.DeviceIoControl(disk,
                    NativeConstants.IOCTL_DISK_SET_DRIVE_LAYOUT_EX, buffer,
                    System.Convert.ToUInt32(buffer.ByteLength), IntPtr.Zero, 0, 0, IntPtr.Zero);

                for (var i = 0; i <= partition_count - 1; i++)
                    Marshal.DestroyStructure(buffer.DangerousGetHandle() + pos + i * partition_struct_size,
                        typeof(PARTITION_INFORMATION_EX));

                Win32Try(rc);
            }
        }

        public static void InitializeDisk(SafeFileHandle disk, PARTITION_STYLE PartitionStyle)
        {
            using (PinnedBuffer<CREATE_DISK_GPT> buffer = new PinnedBuffer<CREATE_DISK_GPT>(1))
            {
                switch (PartitionStyle)
                {
                    case PARTITION_STYLE.MBR:
                    {
                        CREATE_DISK_MBR mbr = new CREATE_DISK_MBR()
                        {
                            PartitionStyle = PARTITION_STYLE.MBR,
                            DiskSignature = NativeCalls.GenerateDiskSignature()
                        };

                        mbr.DiskSignature = mbr.DiskSignature | 0x80808081U;
                        mbr.DiskSignature = mbr.DiskSignature & 0xFEFEFEFFU;

                        buffer.Write(0, mbr);
                        break;
                    }

                    case PARTITION_STYLE.GPT:
                    {
                        CREATE_DISK_GPT gpt = new CREATE_DISK_GPT()
                        {
                            PartitionStyle = PARTITION_STYLE.GPT,
                            DiskId = Guid.NewGuid(),
                            MaxPartitionCount = 128
                        };

                        buffer.Write(0, gpt);
                        break;
                    }

                    default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(PartitionStyle));
                        break;
                    }
                }

                var rc = UnsafeNativeMethods.DeviceIoControl(disk,
                    NativeConstants.IOCTL_DISK_CREATE_DISK, buffer, System.Convert.ToUInt32(buffer.ByteLength),
                    IntPtr.Zero, 0, 0, IntPtr.Zero);

                Win32Try(rc);
            }
        }

        public static void FlushBuffers(SafeFileHandle handle)
        {
            Win32Try(UnsafeNativeMethods.FlushFileBuffers(handle));
        }

        public static bool? GetDiskOffline(SafeFileHandle disk)
        {
            byte attribs_size = 16;

            var attribs = new byte[attribs_size];

            if (UnsafeNativeMethods.DeviceIoControl(disk,
                    NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES, IntPtr.Zero, 0, attribs, attribs_size, 0,
                    IntPtr.Zero))
                return (attribs[8] & 1) != 0;
            else
                return default(Boolean?);
        }

        public static DateTime? TryParseFileTimeUtc(long filetime)
        {
            var MaxFileTime = Date.MaxValue.ToFileTimeUtc();

            if (filetime > 0 && filetime <= MaxFileTime)
                return DateTime.FromFileTimeUtc(filetime);
            else
                return default(DateTime);
        }

        public static bool SetFilePointer(SafeFileHandle file, long distance_to_move, ref long new_file_pointer, uint move_method)
        {
            return UnsafeNativeMethods.SetFilePointerEx(file, distance_to_move, new_file_pointer, move_method);
        }

        public static void SetDiskOffline(SafeFileHandle disk, bool offline)
        {
            byte attribs_size = 40;
            var attribs = new byte[attribs_size];

            attribs[0] = attribs_size;
            attribs[16] = 1;
            if (offline)
                attribs[8] = 1;

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES, attribs, attribs_size, IntPtr.Zero, 0, 0,IntPtr.Zero));
        }

        public static bool? GetDiskReadOnly(SafeFileHandle disk)
        {
            byte attribs_size = 16;
            var attribs = new byte[attribs_size];

            if (UnsafeNativeMethods.DeviceIoControl(disk,
                    NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES, IntPtr.Zero, 0,
                    attribs, attribs_size, 0, IntPtr.Zero))
                return (attribs[8] & 2) != 0;
            else
                return default(Boolean?);
        }

        public static void SetDiskReadOnly(SafeFileHandle disk, bool read_only)
        {
            byte attribs_size = 40;

            var attribs = new byte[attribs_size];

            attribs[0] = attribs_size;
            attribs[16] = 2;
            if (read_only)
                attribs[8] = 2;

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES, attribs,
                attribs_size, IntPtr.Zero, 0, 0, IntPtr.Zero));
        }

        public static void SetVolumeOffline(SafeFileHandle disk, bool offline)
        {
            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                offline
                    ? NativeConstants.IOCTL_VOLUME_OFFLINE
                    : NativeConstants.IOCTL_VOLUME_ONLINE, IntPtr.Zero, 0,
                IntPtr.Zero, 0, 0, IntPtr.Zero));
        }

        public static Exception GetExceptionForNtStatus(Int32 NtStatus)
        {
            return new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(NtStatus));
        }

        public static string GetModuleFullPath(IntPtr hModule)
        {
            StringBuilder str = new StringBuilder(32768);

            var PathLength = UnsafeNativeMethods.GetModuleFileName(hModule, str, str.Capacity);
            if (PathLength == 0)
                throw new Win32Exception();

            return str.ToString(0, PathLength);
        }

        public static IEnumerable<string> EnumerateDiskVolumesMountPoints(string DiskDevice)
        {
            return EnumerateDiskVolumes(DiskDevice).SelectMany(EnumerateVolumeMountPoints);
        }

        public static IEnumerable<string> EnumerateDiskVolumesMountPoints(uint DiskNumber)
        {
            return EnumerateDiskVolumes(DiskNumber).SelectMany(EnumerateVolumeMountPoints);
        }

        public static string GetVolumeNameForVolumeMountPoint(string MountPoint)
        {
            StringBuilder str = new StringBuilder(65536);

            if (UnsafeNativeMethods.GetVolumeNameForVolumeMountPoint(MountPoint, str, str.Capacity) &&
                str.Length > 0)
                return str.ToString();

            if (MountPoint.StartsWith(@"\\?\", StringComparison.Ordinal))
                MountPoint = MountPoint.Substring(4);

            MountPoint = MountPoint.TrimEnd('\\');

            var nt_device_path = QueryDosDevice(MountPoint)?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(nt_device_path))
                return String.Empty;

            /*
             * TODO: Refactor
             */
            /*
            return Aggregate dosdevice
                In QueryDosDevice() Where dosdevice.Length = 44 AndAlso dosdevice.StartsWith("Volume{",
                StringComparison.OrdinalIgnoreCase) Let target = QueryDosDevice(dosdevice) Where target IsNot
                Nothing AndAlso target.Contains(nt_device_path,
                StringComparer.OrdinalIgnoreCase) Select $"\\?\{dosdevice}\" Into FirstOrDefault();
            */
            return String.Empty;
        }

        public static string GetVolumePathName(string path)
        {

            var result = neq char[32767];
            if (!UnsafeNativeMethods.GetVolumePathName(path, result, result.Length))
                throw new IOException($"Failed to get volume name for path '{path}'", new Win32Exception());

            var index = Array.IndexOf(result, new char());

            if (index >= 0)
                return new string(result, 0, index);
            else
                return new string(result);
        }

        public static ScsiAddressAndLength? GetScsiAddressAndLength(string drv)
        {
            UInt32 SizeOfLong = CUInt(PinnedBuffer(Of Long).TypeSize);
            UInt32 SizeOfScsiAddress = CUInt(PinnedBuffer(Of SCSI_ADDRESS).TypeSize);

            try
            {
                using (DiskDevice disk = new DiskDevice(drv, FileAccess.Read))
                {
                    SCSI_ADDRESS ScsiAddress;
                    var rc = UnsafeNativeMethods.DeviceIoControl(disk.SafeFileHandle,
                        NativeConstants.IOCTL_SCSI_GET_ADDRESS, IntPtr.Zero, 0, ScsiAddress, SizeOfScsiAddress,
                        null /* TODO Change to default(_) if this is not a reference type */,
                        null /* TODO Change to default(_) if this is not a reference type */);

                    if (!rc)
                    {
                        Trace.WriteLine(
                            $"IOCTL_SCSI_GET_ADDRESS failed for device {drv}: Error 0x{Marshal.GetLastWin32Error()}");
                        return default(ScsiAddressAndLength?);
                    }

                    long Length;
                    rc = UnsafeNativeMethods.DeviceIoControl(disk.SafeFileHandle,
                        NativeConstants.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, Length, SizeOfLong,
                        null /* TODO Change to default(_) if this is not a reference type */,
                        null /* TODO Change to default(_) if this is not a reference type */);

                    if (!rc)
                    {
                        Trace.WriteLine(
                            $"IOCTL_DISK_GET_LENGTH_INFO failed for device {drv}: Error 0x{Marshal.GetLastWin32Error()}");
                        return default(ScsiAddressAndLength?);
                    }

                    return new ScsiAddressAndLength(ScsiAddress, Length);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception attempting to find SCSI address for device {drv}: {ex.JoinMessages()}");
                return default(ScsiAddressAndLength?);
            }
        }

        public static Dictionary<uint, string> GetDevicesScsiAddresses(ScsiAdapter adapter)
        {
            var q = from device_number in adapter.GetDeviceList()
                let drv = adapter.GetDeviceName(device_number)
                where drv != null
                select device_number;

            return q.ToDictionary(o => o.device_number, o => o.drv);
        }

        public static string GetMountPointBasedPath(string path)
        {
            path.NullCheck(nameof(path));

            const string volume_path_prefix = @"\\?\Volume{00000000-0000-0000-0000-000000000000}\";

            if (path.Length > volume_path_prefix.Length &&
                path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
            {
                var vol = path.Substring(0, volume_path_prefix.Length);
                var mountpoint = EnumerateVolumeMountPoints(vol)?.FirstOrDefault();

                if (mountpoint != null)
                    path = $"{mountpoint}{path.Substring(volume_path_prefix.Length)}";
            }

            return path;
        }

        public static IEnumerable<string> EnumerateVolumeMountPoints(string VolumeName)
        {
            VolumeName.NullCheck(nameof(VolumeName));
            var TargetPath = new char[65536];

            Int32 length;

            if (UnsafeNativeMethods.GetVolumePathNamesForVolumeName(VolumeName, TargetPath, TargetPath.Length,ref length) && length > 2)
                return ParseDoubleTerminatedString(TargetPath, length);

            if (VolumeName.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
                VolumeName = VolumeName.Substring(@"\\?\".Length, 44);
            else if (VolumeName.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase))
                VolumeName = VolumeName.Substring(0, 44);
            else
                return Enumerable.Empty<string>();

            VolumeName = QueryDosDevice(VolumeName)?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(VolumeName))
                return Enumerable.Empty<string>();

            /*
             * TODO : Refactor
             */
            var names = new List<string>();
            /*
            var names = From link In QueryDosDevice() Where link.Length = 2 AndAlso link(1) = ":"c From target
                In QueryDosDevice(link) Where VolumeName.Equals(target,
                StringComparison.OrdinalIgnoreCase) Select $"{link}\"
            */

            return names;
        }

        public static IEnumerable<string> EnumerateDiskVolumes(string DevicePath)
        {
            DevicePath.NullCheck(nameof(DevicePath));

            if (DevicePath.StartsWith(@"\\?\PhysicalDrive", StringComparison.OrdinalIgnoreCase) ||
                DevicePath.StartsWith(@"\\.\PhysicalDrive", StringComparison.OrdinalIgnoreCase))

                return EnumerateDiskVolumes(uint.Parse(DevicePath.Substring(@"\\?\PhysicalDrive".Length)));
            else if (DevicePath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                     DevicePath.StartsWith(@"\\.\", StringComparison.Ordinal))
                return EnumerateVolumeNamesForDeviceObject(QueryDosDevice(DevicePath.Substring(@"\\?\".Length))
                    .First()); // \\?\C: or similar paths to mounted volumes
            else
                return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> EnumerateDiskVolumes(uint DiskNumber)
        {
            return (new VolumeEnumerator()).Where(volumeGuid =>
            {
                try
                {
                    return VolumeUsesDisk(volumeGuid, DiskNumber);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{volumeGuid}: {ex.JoinMessages()}");
                    return false;
                }
            });
        }

        public static IEnumerable<string> EnumerateVolumeNamesForDeviceObject(string DeviceObject)
        {
            if (DeviceObject.EndsWith("}", StringComparison.Ordinal) &&
                DeviceObject.StartsWith(@"\Device\Volume{", StringComparison.Ordinal))
                return
            {
                $@"\\?\{DeviceObject.Substring(@"\Device\".Length)}\"
            }
            ;

            return (new VolumeEnumerator()).Where(volumeGuid =>
            {
                try
                {
                    if (volumeGuid.StartsWith(@"\\?\", StringComparison.Ordinal))
                        volumeGuid = volumeGuid.Substring(4);

                    volumeGuid = volumeGuid.TrimEnd('\\');
                    /*
                     * TODO : Refactor
                     */
                    return true;
                    // return Aggregate target In QueryDosDevice(volumeGuid) Into Any(target.Equals (DeviceObject, StringComparison.OrdinalIgnoreCase))
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{volumeGuid}: {ex.JoinMessages()}");
                    return false;
                }
            });
        }

        public static bool VolumeUsesDisk(string VolumeGuid, uint DiskNumber)
        {
            using (DiskDevice volume = new DiskDevice(VolumeGuid.NullCheck(nameof(VolumeGuid)).TrimEnd('\\'), 0))
            {
                try
                {
                    var extents = GetVolumeDiskExtents(volume.SafeFileHandle);

                    return extents.Any(extent => extent.DiskNumber.Equals(DiskNumber));
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == NativeConstants.ERROR_INVALID_FUNCTION)
                {
                    return false;
                }
            }
        }

        public static void ScanForHardwareChanges()
        {
            ScanForHardwareChanges(null);
        }

        public static UInt32 ScanForHardwareChanges(string rootid)
        {
            UInt32 devInst;
            var status = UnsafeNativeMethods.CM_Locate_DevNode(devInst, rootid, 0);
            if (status != 0)
                return status;

            return UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0);
        }

        public static UInt32? GetDevInst(string devinstName)
        {
            UInt32 devInst;

            var status = UnsafeNativeMethods.CM_Locate_DevNode(devInst, devinstName, 0);

            if (status != 0)
            {
                Trace.WriteLine($"Device '{devinstName}' error 0x{status}");
                return default(UInteger?);
            }

            /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
            /* TODO ERROR: Skipped EndIfDirectiveTrivia */
            return devInst;
        }

        public static UInt32 EnumerateDeviceInstancesForService(string service, out IEnumerable<string> instances)
        {
            Int32 length;
            var status =
                UnsafeNativeMethods.CM_Get_Device_ID_List_Size(length, service,
                    NativeConstants.CM_GETIDLIST_FILTER_SERVICE);
            if (status != 0)
                return status;

            var Buffer = new char[length];


            status = UnsafeNativeMethods.CM_Get_Device_ID_List(service, Buffer,
                System.Convert.ToUInt32(Buffer.Length),
                NativeConstants.CM_GETIDLIST_FILTER_SERVICE);
            if (status != 0)
                return status;

            instances = ParseDoubleTerminatedString(Buffer, length);

            return status;
        }

        public static UInt32 EnumerateDeviceInstancesForSetupClass(string service, out IEnumerable<string> instances)
        {
            Int32 length;
            var status = UnsafeNativeMethods.CM_Get_Device_ID_List_Size(length, service,
                NativeConstants.CM_GETIDLIST_FILTER_SERVICE);
            if (status != 0) return status;

            var Buffer = new char[length];


            status = UnsafeNativeMethods.CM_Get_Device_ID_List(service, Buffer,
                System.Convert.ToUInt32(Buffer.Length),
                NativeConstants.CM_GETIDLIST_FILTER_SERVICE);
            if (status != 0) return status;

            instances = ParseDoubleTerminatedString(Buffer, length);

            return status;
        }

        public static void RestartDevice(Guid devclass, UInt32 devinst)
        {

            // ' get a list of devices which support the given interface
            using (var devinfo = UnsafeNativeMethods.SetupDiGetClassDevs(devclass,
                       null /* TODO Change to default(_) if this is not a reference type */,
                       null /* TODO Change to default(_) if this is not a reference type */,
                       NativeConstants.DIGCF_PROFILE | NativeConstants.DIGCF_DEVICEINTERFACE |
                       NativeConstants.DIGCF_PRESENT))
            {
                if (devinfo.IsInvalid)
                    throw new Exception("Device not found");

                SP_DEVINFO_DATA devInfoData;
                // ' as per DDK docs on SetupDiEnumDeviceInfo
                devInfoData.Initialize();

                // step through the list of devices for this handle
                // get device info at index deviceIndex, the function returns FALSE
                // when there Is no device at the given index.
                var deviceIndex = 0U;

                while (UnsafeNativeMethods.SetupDiEnumDeviceInfo(devinfo, deviceIndex, devInfoData))
                {
                    if (devInfoData.DevInst.Equals(devinst))
                    {
                        var pcp = new SP_PROPCHANGE_PARAMS()
                        {
                            ClassInstallHeader = new SP_CLASSINSTALL_HEADER()
                            {
                                Size = System.Convert.ToUInt32(PinnedBuffer<SP_CLASSINSTALL_HEADER>.TypeSize),
                                InstallFunction = NativeConstants.DIF_PROPERTYCHANGE
                            },
                            HwProfile = 0,
                            Scope = NativeConstants.DICS_FLAG_CONFIGSPECIFIC,
                            StateChange = NativeConstants.DICS_PROPCHANGE
                        };

                        if (UnsafeNativeMethods.SetupDiSetClassInstallParams(devinfo, devInfoData, pcp,
                                PinnedBuffer<SP_PROPCHANGE_PARAMS>.TypeSize) &&
                            UnsafeNativeMethods.SetupDiCallClassInstaller(
                                NativeConstants.DIF_PROPERTYCHANGE, devinfo,
                                devInfoData))
                            return;

                        throw new Exception($"Device restart failed", new Win32Exception());
                    }

                    deviceIndex += 1U;
                }
            }

            throw new Exception("Device not found");
        }

        public static void RunDLLInstallHinfSection(IntPtr OwnerWindow, string InfPath, string InfSection)
        {
            var cmdLine = $"{InfSection} 132 {InfPath}";
            Trace.WriteLine($"RunDLLInstallFromInfSection: {cmdLine}");

            if (InfPath.NullCheck(nameof(InfPath)).Contains(' '))
                throw new ArgumentException("Arguments to this method cannot contain spaces.", nameof(InfPath));

            if (InfSection.NullCheck(nameof(InfSection)).Contains(' '))
                throw new ArgumentException("Arguments to this method cannot contain spaces.", nameof(InfSection));

            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            UnsafeNativeMethods.InstallHinfSection(OwnerWindow, null, cmdLine, 0);
        }

        public static void InstallFromInfSection(IntPtr OwnerWindow, string InfPath, string InfSection)
        {
            Trace.WriteLine($"InstallFromInfSection: InfPath=\"{InfPath}\", InfSection=\"{InfSection}\"");

            // '
            // ' Inf must be a full pathname
            // '
            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            UInt32 ErrorLine;
            var hInf = UnsafeNativeMethods.SetupOpenInfFile(InfPath,
                null /* TODO Change to default(_) if this is not a reference type */, 0x2U, ErrorLine);
            if (hInf.IsInvalid)
                throw new Win32Exception($"Line number: {ErrorLine}");

            using (hInf)

                Win32Try(UnsafeNativeMethods.SetupInstallFromInfSection(OwnerWindow, hInf, InfSection,
                    0x1FFU,
                    IntPtr.Zero,
                    null /* TODO Change to default(_) if this is not a reference type */, 0x4U, () => 1,
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */,
                    null /* TODO Change to default(_) if this is not a reference type */));
        }

        public const  UInt32 DIF_REGISTERDEVICE = 0x19U;
        public const UInt32 DIF_REMOVE = 0x5U;

        public static void CreateRootPnPDevice(IntPtr OwnerWindow, string InfPath, string hwid,
            bool ForceReplaceExistingDrivers, out bool RebootRequired)
        {
            Trace.WriteLine($"CreateOrUpdateRootPnPDevice: InfPath=\"{InfPath}\", hwid=\"{hwid}\"");

            // '
            // ' Inf must be a full pathname
            // '
            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            // '
            // ' List of hardware ID's must be double zero-terminated
            // '
            var hwIdList = Encoding.Unicode.GetBytes(hwid);

            // '
            // ' Use the INF File to extract the Class GUID.
            // '
            Guid ClassGUID;
            var ClassName = new char[31];


            Win32Try(UnsafeNativeMethods.SetupDiGetINFClass(InfPath, ClassGUID, ClassName,
                System.Convert.ToUInt32(ClassName.Length), 0));

            Trace.WriteLine(
                $"CreateOrUpdateRootPnPDevice: ClassGUID=""{ClassGUID}"", ClassName=""{new string(ClassName)}""");

            // '
            // ' Create the container for the to-be-created Device Information Element.
            // '
            var DeviceInfoSet = UnsafeNativeMethods.SetupDiCreateDeviceInfoList(ClassGUID, OwnerWindow);
            if (DeviceInfoSet.IsInvalid)
                throw new Win32Exception();

            using (DeviceInfoSet)
            {

                // Now create the element. Use the Class GUID and Name from the INF file.
                SP_DEVINFO_DATA DeviceInfoData;
                DeviceInfoData.Initialize();
                Win32Try(UnsafeNativeMethods.SetupDiCreateDeviceInfo(DeviceInfoSet, ClassName, ClassGUID,
                    null /* TODO Change to default(_) if this is not a reference type */, OwnerWindow, 0x1U,
                    DeviceInfoData));

                // '
                // ' Add the HardwareID to the Device's HardwareID property.
                // '
                Win32Try(UnsafeNativeMethods.SetupDiSetDeviceRegistryProperty(DeviceInfoSet,
                    DeviceInfoData,
                    0x1U, hwIdList,
                    System.Convert.ToUInt32(hwIdList.Length)));

                // '
                // ' Transform the registry element into an actual devnode
                // ' in the PnP HW tree.
                // '
                Win32Try(UnsafeNativeMethods.SetupDiCallClassInstaller(DIF_REGISTERDEVICE, DeviceInfoSet,
                    DeviceInfoData));
            }

            // '
            // ' update the driver for the device we just created
            // '
            UpdateDriverForPnPDevices(OwnerWindow, InfPath, hwid, ForceReplaceExistingDrivers, out RebootRequired);
        }

        public static IEnumerable<UInt32> EnumerateChildDevices(UInt32 devInst)
        {
            uint child;

            var rc = UnsafeNativeMethods.CM_Get_Child(child, devInst, 0);

            while (rc == 0)
            {
                Trace.WriteLine($"Found child devinst: {child}");

                yield return child;

                rc = UnsafeNativeMethods.CM_Get_Sibling(child, child, 0);
            }
        }

        public static string GetPhysicalDeviceObjectNtPath(string devInstName)
        {
            var devinst = GetDevInst(devInstName);

            if (!devinst.HasValue)
                return null;

            return GetPhysicalDeviceObjectNtPath(devinst.Value);
        }

        public static string GetPhysicalDeviceObjectNtPath(UInt32 devInst)
        {
            RegistryValueKind regtype;
            var buffer = new byte[518];

            var buffersize = buffer.Length;

            var rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_Property(devInst,
                CmDevNodeRegistryProperty.CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME, regtype, buffer, buffersize, 0);

            if (rc != 0)
            {
                Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc}");
                return null;
            }

            var name = Encoding.Unicode.GetString(buffer, 0, buffersize - 2);

            /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
            /* TODO ERROR: Skipped EndIfDirectiveTrivia */
            return name;
        }

        public static IEnumerable<string> GetDeviceRegistryProperty(UInt32 devInst, CmDevNodeRegistryProperty prop)
        {
            RegistryValueKind regtype;
            var buffer = new byte[518];


            var buffersize = buffer.Length;

            var rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_Property(devInst, prop, regtype, buffer,
                buffersize, 0);

            if (rc != 0)
            {
                Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc}");
                return null;
            }

            var name = ParseDoubleTerminatedString(buffer, buffersize);

            return name;
        }

        public static IEnumerable<string> EnumerateWin32DevicePaths(string nt_device_path)
        {
            return from dosdevice in QueryDosDevice()
                where QueryDosDevice(dosdevice).Contains(nt_device_path, StringComparer.OrdinalIgnoreCase)
                select $@"\\?\{dosdevice}";
        }

        public static IEnumerable<string> EnumerateRegisteredFilters(UInt32 devInst)
        {
            RegistryValueKind regtype;


            var buffer = new byte[65535];

            var buffersize = buffer.Length;

            var rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_Property(devInst,
                CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS, regtype, buffer, buffersize, 0);

            if (rc == NativeConstants.CR_NO_SUCH_VALUE)
                return Enumerable.Empty<string>();
            else if (rc != 0)
            {
                var msg = $"Error getting registry property for device. Status=0x{rc}";
                throw new IOException(msg);
            }

            return ParseDoubleTerminatedString(buffer, buffersize);
        }

        // Switched to querying registry directly instead. CM_Get_Class_Registry_Property seems to
        // return 0x13 CR_FAILURE on Win7.
        public static string[] GetRegisteredFilters(Guid devClass)
        {
            using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Class\{devClass}"))
            {
                return key?.GetValue("UpperFilters") as string[];
            }
        }

        public static void SetRegisteredFilters(UInt32 devInst, string[] filters)
        {
            var str = string.Join(new char(), filters) + new char() + new char();
            var buffer = Encoding.Unicode.GetBytes(str);
            var buffersize = buffer.Length;

            var rc = UnsafeNativeMethods.CM_Set_DevNode_Registry_Property(devInst,
                CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS, buffer, buffersize, 0);

            if (rc != 0)
                throw new Exception($"Error setting registry property for device. Status=0x{rc}");
        }

        public static void SetRegisteredFilters(Guid devClass, string[] filters)
        {
            var str = string.Join(new char(), filters) + new char() + new char();
            var buffer = Encoding.Unicode.GetBytes(str);
            var buffersize = buffer.Length;

            var rc = UnsafeNativeMethods.CM_Set_Class_Registry_Property(devClass,
                CmClassRegistryProperty.CM_CRP_UPPERFILTERS, buffer, buffersize, 0);

            if (rc != 0)
                throw new Exception($"Error setting registry property for class {devClass}. Status=0x{rc}");
        }

        public static bool AddFilter(UInt32 devInst, string driver)
        {
            var filters = EnumerateRegisteredFilters(devInst).ToArray();

            if (filters.Any(f => f.Equals(driver, StringComparison.OrdinalIgnoreCase)))
            {
                Trace.WriteLine($"Filter '{driver}' already registered for devinst {devInst}");

                return false;
            }

            Trace.WriteLine($"Registering filter '{driver}' for devinst {devInst}");

            Array.Resize(ref filters, filters.Length + 1);

            filters[filters.Length - 1] = driver;

            SetRegisteredFilters(devInst, filters);

            return true;
        }

        public static bool AddFilter(Guid devClass, string driver, bool addfirst)
        {
            driver.NullCheck(nameof(driver));

            var filters = GetRegisteredFilters(devClass);

            if (filters == null)

                /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
                /* TODO ERROR: Skipped ElseDirectiveTrivia */
                filters = Array.Empty<string>();
            else if (addfirst && driver.Equals(filters.FirstOrDefault(), StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"Filter '{driver}' already registered first for class {devClass}");
                return false;
            }
            else if ((!addfirst) && driver.Equals(filters.LastOrDefault(), StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"Filter '{driver}' already registered last for class {devClass}");
                return false;
            }

            List<string> filter_list = new List<string>(filters);

            filter_list.RemoveAll(f => f.Equals(driver, StringComparison.OrdinalIgnoreCase));

            if (addfirst)
                filter_list.Insert(0, driver);
            else
                filter_list.Add(driver);

            filters = filter_list.ToArray();

            Trace.WriteLine($"Registering filters '{string.Join(",", filters)}' for class {devClass}");

            SetRegisteredFilters(devClass, filters);

            return true;
        }

        public static bool RemoveFilter(UInt32 devInst, string driver)
        {
            var filters = EnumerateRegisteredFilters(devInst).ToArray();

            if (filters == null || filters.Length == 0)
            {
                Trace.WriteLine($"No filters registered for devinst {devInst}");
                return false;
            }

            var newfilters = filters.Where(f => !f.Equals(driver, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (newfilters.Length == filters.Length)
            {
                Trace.WriteLine($"Filter '{driver}' not registered for devinst {devInst}");
                return false;
            }

            Trace.WriteLine($"Removing filter '{driver}' from devinst {devInst}");

            SetRegisteredFilters(devInst, newfilters);

            return true;
        }

        public static bool RemoveFilter(Guid devClass, string driver)
        {
            var filters = GetRegisteredFilters(devClass);

            if (filters == null)
            {
                Trace.WriteLine($"No filters registered for class {devClass}");
                return false;
            }

            var newfilters = filters.Where(f => !f.Equals(driver, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (newfilters.Length == filters.Length)
            {
                Trace.WriteLine($"Filter '{driver}' not registered for class {devClass}");
                return false;
            }

            Trace.WriteLine($"Removing filter '{driver}' from class {devClass}");

            SetRegisteredFilters(devClass, newfilters);

            return true;
        }

        public static int RemovePnPDevice(IntPtr OwnerWindow, string hwid)
        {
            Trace.WriteLine($"RemovePnPDevice: hwid='{hwid}'");


            var DeviceInfoSet = UnsafeNativeMethods.SetupDiCreateDeviceInfoList(IntPtr.Zero, OwnerWindow);
            if (DeviceInfoSet.IsInvalid)
                throw new Win32Exception("SetupDiCreateDeviceInfoList");

            using (DeviceInfoSet)
            {
                if (!UnsafeNativeMethods.SetupDiOpenDeviceInfo(DeviceInfoSet, hwid, OwnerWindow, 0, IntPtr.Zero))
                    return 0;

                /*
                 * TODO: Refactor
                 */
                /*
                SP_DEVINFO_DATA DeviceInfoData;
                DeviceInfoData.Initialize();

                uint i;
                int done;
                do
                {
                    if (!UnsafeNativeMethods.SetupDiEnumDeviceInfo(DeviceInfoSet, ref i, DeviceInfoData))
                    {
                        if (i == 0)
                            throw new Win32Exception("SetupDiEnumDeviceInfo");
                        else
                            return done;
                    }

                    if (UnsafeNativeMethods.SetupDiCallClassInstaller(DIF_REMOVE, DeviceInfoSet, DeviceInfoData)) {}
                        done += 1;
                    i += 1U;
                } while (true);
                */

            }
        }

        public static void UpdateDriverForPnPDevices(IntPtr OwnerWindow, string InfPath, string hwid, bool forceReplaceExisting, ref bool RebootRequired)
        {
            Trace.WriteLine(
                $"UpdateDriverForPnPDevices: InfPath=\"{InfPath}\", hwid=\"{hwid}\", forceReplaceExisting={forceReplaceExisting}");

            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);
            Win32Try(UnsafeNativeMethods.UpdateDriverForPlugAndPlayDevices(OwnerWindow, hwid, InfPath, forceReplaceExisting ? 0x1U : 0x0U, RebootRequired));
        }

        public static string SetupCopyOEMInf(string InfPath, bool NoOverwrite)
        {
            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            StringBuilder destName = new StringBuilder(260);

            Win32Try(UnsafeNativeMethods.SetupCopyOEMInf(InfPath,null , 0, NoOverwrite ? 0x8U : 0x0U, destName, destName.Capacity, null ,null ));

            return destName.ToString();
        }

        public static void DriverPackagePreinstall(string InfPath)
        {
            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            var errcode = UnsafeNativeMethods.DriverPackagePreinstall(InfPath, 1);
            if (errcode != 0)
                throw new Win32Exception(errcode);
        }

        public static void DriverPackageInstall(string InfPath, ref bool NeedReboot)
        {

            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            var errcode = UnsafeNativeMethods.DriverPackageInstall(InfPath, 1, null , NeedReboot);
            if (errcode != 0)
                throw new Win32Exception(errcode);
        }

        public static void DriverPackageUninstall(string InfPath, DriverPackageUninstallFlags Flags, ref bool NeedReboot)
        {
            InfPath = Path.GetFullPath(InfPath);
            if (!File.Exists(InfPath))
                throw new FileNotFoundException("File not found", InfPath);

            var errcode = UnsafeNativeMethods.DriverPackageUninstall(InfPath, Flags, null , NeedReboot);
            if (errcode != 0)
                throw new Win32Exception(errcode);
        }

        public static bool MapFileAndCheckSum(string file, out int headerSum, out int checkSum)
        {
            return UnsafeNativeMethods.MapFileAndCheckSum(file, headerSum, checkSum) == 0;
        }

        /// <summary>
        ///  Re-enumerates partitions on all disk drives currently connected to the system. No exceptions are
        ///  thrown on error, but any exceptions from underlying API calls are logged to trace log.
        ///  </summary>
        public static void UpdateDiskProperties()
        {
            foreach (var diskdevice in from device in QueryDosDevice()
                     where device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) ||
                           device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)
                     select device)
            {
                try
                {
                    using (var device = OpenFileHandle($@"\\?\{diskdevice}", 0, FileShare.ReadWrite, FileMode.Open,
                               Overlapped: false))
                    {
                        if (!UpdateDiskProperties(device, throwOnFailure: false))
                            Trace.WriteLine(
                                $"Error updating disk properties for {diskdevice}: {new Win32Exception().Message}");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error updating disk properties for {diskdevice}: {ex.JoinMessages()}");
                }
            }
        }

        /// <summary>
        ///  Re-enumerates partitions on a disk device with a specified SCSI address. No
        ///  exceptions are thrown on error, but any exceptions from underlying API calls are
        ///  logged to trace log.
        ///  </summary>
        ///  <returns>Returns a value indicating whether operation was successful or not.</returns>
        public static bool UpdateDiskProperties(SCSI_ADDRESS ScsiAddress)
        {
            try
            {
                using (var devicehandle = OpenDiskByScsiAddress(ScsiAddress,
                           null /* TODO Change to default(_) if this is not a reference type */).Value)
                {
                    var rc = UpdateDiskProperties(devicehandle, throwOnFailure: false);

                    if (!rc)
                        Trace.WriteLine(
                            $"Updating disk properties failed for {ScsiAddress}: {new Win32Exception().Message}");

                    return rc;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error updating disk properties for {ScsiAddress}: {ex.JoinMessages()}");
            }

            return false;
        }

        public static bool UpdateDiskProperties(SafeFileHandle devicehandle, bool throwOnFailure)
        {
            var rc = UnsafeNativeMethods.DeviceIoControl(devicehandle,
                NativeConstants.IOCTL_DISK_UPDATE_PROPERTIES,
                IntPtr.Zero, 0U, IntPtr.Zero, 0U, 0U, IntPtr.Zero);

            if (!rc && throwOnFailure)
                throw new Win32Exception();

            return rc;
        }

        /// <summary>
        ///  Re-enumerates partitions on a disk device with a specified device path. No exceptions are thrown on error, but any exceptions from underlying API calls are logged to trace log.
        ///  </summary>
        ///  <returns>Returns a value indicating whether operation was successful or not.</returns>

        public static bool UpdateDiskProperties(string DevicePath)
        {
            try
            {
                using (var devicehandle =
                       OpenFileHandle(DevicePath, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, 0))
                {
                    var rc = UnsafeNativeMethods.DeviceIoControl(devicehandle,
                        NativeConstants.IOCTL_DISK_UPDATE_PROPERTIES,
                        IntPtr.Zero, 0U, IntPtr.Zero, 0U, 0U, IntPtr.Zero);

                    if (!rc)
                        Trace.WriteLine(
                            $"Updating disk properties failed for {DevicePath}: {new Win32Exception().Message}");

                    return rc;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error updating disk properties for {DevicePath}: {ex.JoinMessages()}");
            }

            return false;
        }

        /// <summary>
        ///  Opens a disk device with a specified SCSI address and returns both name and an open handle.
        ///  </summary>
        public static KeyValuePair<string, SafeFileHandle> OpenDiskByScsiAddress(SCSI_ADDRESS ScsiAddress,
            FileAccess AccessMode)
        {
            var dosdevs = QueryDosDevice();

            var rawdevices = dosdevs.Where(device =>
                device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) ||
                device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)).Select(c=> c);


            var volumedevices = dosdevs.Where(device => device.Length == 2 && device[1].Equals(':')).Select(c => c);
            /*
             * TODO; Refactor
             */
            /*
            var filter = () diskdevice =>
            {
                try
                {
                    var devicehandle = OpenFileHandle(diskdevice, AccessMode, FileShare.ReadWrite, FileMode.Open,
                        Overlapped: false);

                    try
                    {
                        var Address = GetScsiAddress(devicehandle);

                        if (!Address.HasValue || !Address.Value.Equals(ScsiAddress))
                        {
                            devicehandle.Dispose();

                            return default(KeyValuePair<String, SafeFileHandle>);
                        }

                        Trace.WriteLine($"Found {diskdevice} with SCSI address {Address}");

                        return new KeyValuePair<string, SafeFileHandle>(diskdevice, devicehandle);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Exception while querying SCSI address for {diskdevice}: {ex.JoinMessages()}");

                        devicehandle.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Exception while opening {diskdevice}: {ex.JoinMessages()}");
                }

                return default(KeyValuePair(Of String, SafeFileHandle));
            };

            var dev = Aggregate anydevice In rawdevices.Concat(volumedevices) Select seldevice =
                filter($"\\?\{anydevice}") Into FirstOrDefault(seldevice.Key IsNot Nothing)

            if (dev.Key == null)
                throw new DriveNotFoundException($"No physical drive found with SCSI address: {ScsiAddress}");

            return dev;
            */
            return new KeyValuePair<string, SafeFileHandle>();
        }


        public static bool TestFileOpen(string path)
        {
            using (var handle = UnsafeNativeMethods.CreateFile(path, FileSystemRights.ReadAttributes, 0,
                       IntPtr.Zero,
                       NativeConstants.OPEN_EXISTING, 0, IntPtr.Zero))
            {
                return !handle.IsInvalid;
            }
        }

        public static void CreateHardLink(string existing, string newlink)
        {
            Win32Try(UnsafeNativeMethods.CreateHardLink(newlink, existing,
                null /* TODO Change to default(_) if this is not a reference type */));
        }

        public static void MoveFile(string existing, string newname)
        {
            Win32Try(UnsafeNativeMethods.MoveFile(existing, newname));
        }

        public static OperatingSystem GetOSVersion()
        {
            var os_version = OSVERSIONINFOEX.Initalize();

            var status = UnsafeNativeMethods.RtlGetVersion(os_version);

            if (status < 0)
                throw new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(status));

            return new OperatingSystem(os_version.PlatformId,
                new Version(os_version.MajorVersion, os_version.MinorVersion, os_version.BuildNumber,
                    System.Convert.ToInt32(os_version.ServicePackMajor) << 16 |
                    System.Convert.ToInt32(os_version.ServicePackMinor)));
        }

        public static IEnumerable<string> ParseDoubleTerminatedString(Array bbuffer, int byte_count)
        {
            if (bbuffer == null)
                return Enumerable.Empty<string>();

            byte_count = Math.Min(Buffer.ByteLength(bbuffer), byte_count);

            var cbuffer = bbuffer as char[];

            if (cbuffer == null)
            {
                var cbuffer = new char[byte_count];


                Buffer.BlockCopy(bbuffer, 0, cbuffer, 0, byte_count);
            }

            return ParseDoubleTerminatedString(cbuffer, byte_count >> 1);
        }

        public static IEnumerable<string> ParseDoubleTerminatedString(char[] buffer, int length)
        {
            if (buffer == null)
                yield break;

            length = Math.Min(length, buffer.Length);

            var i = 0;

            while (i < length)
            {
                var pos = Array.IndexOf(buffer, new char(), i, length - i);

                if (pos < 0)
                {
                    yield return new string(buffer, i, length - i);
                    yield break;
                }
                else if (pos == i)
                    yield break;
                else if (pos > i)
                {
                    yield return new string(buffer, i, pos - i);
                    i = pos + 1;
                }
            }
        }
    }
}
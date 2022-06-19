using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using ImageMounter.Interop.IO;
using ImageMounter.IO.Native;
using Microsoft.Win32.SafeHandles;

/* TODO ERROR: Skipped WarningDirectiveTrivia */
namespace ImageMounter.IO
{
    public static class NativeStruct
    {

        public static bool IsOsWindows { get; } = true;
        public static long GetFileSize(string path)
        {

            return NativeFileIO.GetFileSize(path);
        }

        public static byte[] ReadAllBytes(string path)
        {

            /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped EndIfDirectiveTrivia */
            using (var stream = NativeFileIO.OpenFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, (FileOptions)NativeConstants.FILE_FLAG_BACKUP_SEMANTICS))
            {
                
 var               buffer = new byte[stream.Lengt];

 
                if (stream.Read(buffer, 0, buffer.Length) != stream.Length)
                    throw new IOException($"Incomplete read from '{path}'");

                return buffer;
            }
        }

    
        private readonly static Dictionary<string, long> KnownFormats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "nrg",
                600 << 9
            },
            {
                "sdi",
                8 << 9
            }
        };

        /// <summary>
        /// Checks if filename contains a known extension for which PhDskMnt knows of a constant offset value. That value can be
        /// later passed as Offset parameter to CreateDevice method.
        /// </summary>
        /// <param name="ImageFile">Name of disk image file.</param>
        public static long GetOffsetByFileExt(string ImageFile) => KnownFormats.TryGetValue(Path.GetExtension(ImageFile), out var offset) ? offset : 0;

        /// <summary>
        /// Returns sector size typically used for image file name extensions. Returns 2048 for
        /// .iso, .nrg and .bin. Returns 512 for all other file name extensions.
        /// </summary>
        /// <param name="ImageFile">Name of disk image file.</param>
        public static uint GetSectorSizeFromFileName(string imagefile)
        {
            imagefile.NullCheck(nameof(imagefile));

            if (imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) || imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) || imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                return 2048;
            else
                return 512;
        }

        private readonly static Dictionary<ulong, string> multipliers = new Dictionary<ulong, string>() { { 1UL << 60, " EB" }, { 1UL << 50, " PB" }, { 1UL << 40, " TB" }, { 1UL << 30, " GB" }, { 1UL << 20, " MB" }, { 1UL << 10, " KB" } };

        public static string FormatBytes(ulong size)
        {
            foreach (var m in multipliers)
            {
                if (size >= m.Key)
                    return $"{size / (double)m.Key}{m.Value}";
            }

            return $"{size} byte";
        }

        public static string FormatBytes(ulong size, int precision)
        {
            foreach (var m in multipliers)
            {
                if (size >= m.Key)
                    return $"{(size / (double)m.Key).ToString($"0.{new string('0', precision - 1)}")}{m.Value}";
            }

            return $"{size} byte";
        }

        public static string FormatBytes(long size)
        {
            foreach (var m in multipliers)
            {
                if (Math.Abs(size) >= m.Key)
                    return $"{size / (double)m.Key}{m.Value}";
            }

            if (size == 1)
                return $"{size} byte";
            else
                return $"{size} bytes";
        }

        public static string FormatBytes(long size, int precision)
        {
            foreach (var m in multipliers)
            {
                if (size >= m.Key)
                    return $"{(size / (double)m.Key).ToString("0." + new string('0', precision - 1))}{m.Value}";
            }

            return $"{size} byte";
        }

        /// <summary>
        /// Checks if Flags specifies a read only virtual disk.
        /// </summary>
        /// <param name="Flags">Flag field to check.</param>
        public static bool IsReadOnly(this DeviceFlags Flags)
        {
            return Flags.HasFlag(DeviceFlags.ReadOnly);
        }

        /// <summary>
        /// Checks if Flags specifies a removable virtual disk.
        /// </summary>
        /// <param name="Flags">Flag field to check.</param>
        public static bool IsRemovable(this DeviceFlags Flags)
        {
            return Flags.HasFlag(DeviceFlags.Removable);
        }

        /// <summary>
        /// Checks if Flags specifies a modified virtual disk.
        /// </summary>
        /// <param name="Flags">Flag field to check.</param>
        public static bool IsModified(this DeviceFlags Flags)
        {
            return Flags.HasFlag(DeviceFlags.Modified);
        }

        /// <summary>
        /// Gets device type bits from a Flag field.
        /// </summary>
        /// <param name="Flags">Flag field to check.</param>
        public static DeviceFlags GetDeviceType(this DeviceFlags Flags)
        {
            return (DeviceFlags)Flags & 0xF0U;
        }

        /// <summary>
        /// Gets disk type bits from a Flag field.
        /// </summary>
        /// <param name="Flags">Flag field to check.</param>
        public static DeviceFlags GetDiskType(this DeviceFlags Flags)
        {
            return (DeviceFlags)Flags & 0xF00U;
        }

        /// <summary>
        /// Gets proxy type bits from a Flag field.
        /// </summary>
        /// <param name="Flags">Flag field to check.</param>
        public static DeviceFlags GetProxyType(this DeviceFlags Flags)
        {
            return (DeviceFlags)Flags & 0xF000U;
        }

     
    }
}

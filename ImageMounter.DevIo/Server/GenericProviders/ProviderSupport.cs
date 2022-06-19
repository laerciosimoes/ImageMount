// '''' ProviderSupport.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using Arsenal.ImageMounter.Devio.Server.SpecializedProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO;
using DiscUtils;
using ImageMounter;
using ImageMounter.Devio.Interop;
using ImageMounter.Devio.Interop.Server.GenericProviders;
using ImageMounter.DevIo.Server.GenericProviders;
using ImageMounter.IO;
using Microsoft.Win32.SafeHandles;

namespace Server.GenericProviders
{
    public static class ProviderSupport
    {
        public static int ImageConversionIoBufferSize { get; set; } = 2 << 20;

        public static string[] GetMultiSegmentFiles(string FirstFile)
        {
            var pathpart = Path.GetDirectoryName(FirstFile);
            var filepart = Path.GetFileNameWithoutExtension(FirstFile);
            var extension = Path.GetExtension(FirstFile);
            string[] foundfiles = null;

            if (extension.EndsWith("01", StringComparison.Ordinal) || extension.EndsWith("00", StringComparison.Ordinal))
            {
                var start = extension.Length - 3;

                while (start >= 0 && char.IsDigit(extension, start))
                    start -= 1;

                start += 1;

                var segmentnumberchars = new string('?', extension.Length - start);
                var namebase = filepart + extension.Remove(start);
                var pathbase = Path.Combine(Path.GetDirectoryName(FirstFile), namebase);
                var dir_name = Path.GetDirectoryName(FirstFile);
                var dir_pattern = namebase + segmentnumberchars;

                if (string.IsNullOrWhiteSpace(dir_name))
                    dir_name = ".";

                try
                {
                    foundfiles = Directory.GetFiles(dir_name, dir_pattern);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed enumerating files '{dir_pattern}' in directory '{dir_name}'", ex);
                }

                for (var i = 0; i <= foundfiles.Length - 1; i++)
                    foundfiles[i] = Path.GetFullPath(foundfiles[i]);

                Array.Sort(foundfiles, StringComparer.Ordinal);
            }
            else if (File.Exists(FirstFile))
                foundfiles = new[] { FirstFile };

            if (foundfiles == null || foundfiles.Length == 0)
                throw new FileNotFoundException("Image file not found", FirstFile);

            return foundfiles;
        }

        public static void ConvertToDiscUtilsImage(this IDevioProvider provider, string outputImage, string type, string OutputImageVariant, CompletionPosition completionPosition, CancellationToken cancel)
        {
            using (var builder = VirtualDisk.CreateDisk(type, OutputImageVariant, outputImage, provider.Length, Geometry.FromCapacity(provider.Length, System.Convert.ToInt32(provider.SectorSize)), null/* TODO Change to default(_) if this is not a reference type */))
            {
                provider.WriteToSkipEmptyBlocks(builder.Content, ImageConversionIoBufferSize, skipWriteZeroBlocks: true, hashResults: null/* TODO Change to default(_) if this is not a reference type */, completionPosition: completionPosition, cancel: cancel);
            }
        }

        public static void ConvertToRawImage(this IDevioProvider provider, string outputImage, string OutputImageVariant, CompletionPosition completionPosition, CancellationToken cancel)
        {
            using (FileStream target = new FileStream(outputImage, FileMode.Create, FileAccess.Write, FileShare.Delete, ImageConversionIoBufferSize))
            {
                if ("fixed".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase))
                {
                }
                else if ("dynamic".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase))
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        throw new PlatformNotSupportedException("Sparse files not supported on target platform or OS");

                    try
                    {
                        NativeFileIO.SetFileSparseFlag(target.SafeFileHandle, true);
                    }
                    catch (Exception ex)
                    {
                        throw new NotSupportedException("Sparse files not supported on target platform or OS", ex);
                    }
                }
                else
                    throw new ArgumentException($"Value {OutputImageVariant} not supported as output image variant. Valid values are fixed or dynamic.");

                provider.WriteToSkipEmptyBlocks(target, ImageConversionIoBufferSize, skipWriteZeroBlocks: true, hashResults: null/* TODO Change to default(_) if this is not a reference type */, completionPosition: completionPosition, cancel: cancel);
            }
        }

        public static void WriteToPhysicalDisk(this IDevioProvider provider, string outputDevice, CompletionPosition completionPosition, CancellationToken cancel)
        {
            using (DiskDevice disk = new DiskDevice(outputDevice, FileAccess.ReadWrite))
            {
                provider.WriteToSkipEmptyBlocks(disk.GetRawDiskStream(), ImageConversionIoBufferSize, skipWriteZeroBlocks: false, hashResults: null/* TODO Change to default(_) if this is not a reference type */, completionPosition: completionPosition, cancel: cancel);
            }
        }

        public static void WriteToSkipEmptyBlocks(this IDevioProvider source, Stream target, int buffersize, bool skipWriteZeroBlocks, Dictionary<string, byte[]> hashResults, CompletionPosition completionPosition, CancellationToken cancel)
        {
            using (DisposableDictionary<string, HashAlgorithm> hashProviders = new DisposableDictionary<string, HashAlgorithm>(StringComparer.OrdinalIgnoreCase))
            {
                if (hashResults != null)
                {
                    foreach (var hashName in hashResults.Keys)
                    {
                        var hashProvider = HashAlgorithm.Create(hashName);
                        hashProvider.Initialize();
                        hashProviders.Add(hashName, hashProvider);
                    }
                }


                var buffer = new byte[buffersize ];

                var count = 0;

                var source_position = 0L;

                do
                {
                    cancel.ThrowIfCancellationRequested();
                    var length_to_read = System.Convert.ToInt32(Math.Min(buffer.Length, source.Length - source_position));
                    if (length_to_read == 0)
                        break;
                    count = source.Read(buffer, 0, length_to_read, source_position);
                    if (count == 0)
                        throw new IOException($"Read error, {length_to_read} bytes from {source_position}");
                    Parallel.ForEach(hashProviders.Values, hashProvider => hashProvider.TransformBlock(buffer, 0, count, null, 0));
                    source_position += count;
                    if (completionPosition != null)
                        completionPosition.LengthComplete = source_position;
                    if (skipWriteZeroBlocks && buffer.IsBufferZero())
                        target.Seek(count, SeekOrigin.Current);
                    else
                    {
                        cancel.ThrowIfCancellationRequested();
                        target.Write(buffer, 0, count);
                    }
                }
                while (true);

                if (target.Length != target.Position)
                    target.SetLength(target.Position);

                foreach (var hashProvider in hashProviders)
                {
                    /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
                    hashProvider.Value.TransformFinalBlock(
                    {
                    }, 0, 0);
                    /* TODO ERROR: Skipped EndIfDirectiveTrivia */
                    hashResults[hashProvider.Key] = hashProvider.Value.Hash;
                }
            }
        }
    }
}

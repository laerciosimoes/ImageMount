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
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DiscUtils;
using DiscUtils.Streams;
using ImageMounter;
using ImageMounter.Devio.Interop.Client;
using ImageMounter.Devio.Interop.Server.GenericProviders;
using ImageMounter.DevIo.Server.GenericProviders;
using ImageMounter.DevIo.Server.Interaction;
using ImageMounter.DevIo.Server.Services;
using ImageMounter.Interop.IO;
using ImageMounter.IO;
using Server.SpecializedProviders;

namespace Server.Interaction
{

    /// <summary>
    /// Support routines for creating provider and service instances given a known proxy provider.
    /// </summary>
    public sealed class DevioServiceFactory
    {
        private DevioServiceFactory()
        {
        }

        private static readonly Dictionary<ProxyType, ReadOnlyCollection<VirtualDiskAccess>>
            SupportedVirtualDiskAccess = new Dictionary<ProxyType, ReadOnlyCollection<VirtualDiskAccess>>()
            {
                {
                    ProxyType.None,
                    Array.AsReadOnly(new[]
                    {
                        VirtualDiskAccess.ReadOnly, VirtualDiskAccess.ReadWriteOriginal,
                        VirtualDiskAccess.ReadOnlyFileSystem, VirtualDiskAccess.ReadWriteFileSystem
                    })
                },
                {
                    ProxyType.MultiPartRaw,
                    Array.AsReadOnly(new[]
                    {
                        VirtualDiskAccess.ReadOnly, VirtualDiskAccess.ReadWriteOriginal,
                        VirtualDiskAccess.ReadOnlyFileSystem, VirtualDiskAccess.ReadWriteFileSystem
                    })
                },
                {
                    ProxyType.DiscUtils,
                    Array.AsReadOnly(new[]
                    {
                        VirtualDiskAccess.ReadOnly, VirtualDiskAccess.ReadWriteOriginal,
                        VirtualDiskAccess.ReadWriteOverlay, VirtualDiskAccess.ReadOnlyFileSystem,
                        VirtualDiskAccess.ReadWriteFileSystem
                    })
                }
            };

        private static readonly string[] NotSupportedFormatsForWriteOverlay = new[]
        {
            ".vdi",
            ".xva"
        };


        /// <summary>
        /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file. Once that is done, this method
        /// automatically calls Arsenal Image Mounter to create a virtual disk device for this
        /// image file.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="Adapter">Open ScsiAdapter object for communication with Arsenal Image Mounter.</param>
        /// <param name="Flags">Additional flags to pass to ScsiAdapter.CreateDevice(). For example,
        /// this could specify a flag for read-only mounting.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static DevioServiceBase AutoMount(string Imagefile, ScsiAdapter Adapter, ProxyType Proxy,
            DeviceFlags Flags, VirtualDiskAccess DiskAccess)
        {
            if (Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ||
                Imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) ||
                Imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                Flags = Flags | DeviceFlags.DeviceTypeCD;

            var Service = GetService(Imagefile, DiskAccess, Proxy);

            Service.StartServiceThreadAndMount(Adapter, Flags);

            return Service;
        }

        /// <summary>
        /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file. Once that is done, this method
        /// automatically calls Arsenal Image Mounter to create a virtual disk device for this
        /// image file.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="Adapter">Open ScsiAdapter object for communication with Arsenal Image Mounter.</param>
        /// <param name="Flags">Additional flags to pass to ScsiAdapter.CreateDevice(). For example,
        /// this could specify a flag for read-only mounting.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static DevioServiceBase AutoMount(string Imagefile, ScsiAdapter Adapter, ProxyType Proxy,
            DeviceFlags Flags)
        {
            FileAccess DiskAccess;

            if (!Flags.HasFlag(DeviceFlags.ReadOnly))
                DiskAccess = FileAccess.ReadWrite;
            else
                DiskAccess = FileAccess.Read;

            if (Imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ||
                Imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) ||
                Imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                Flags = Flags | DeviceFlags.DeviceTypeCD;

            var Service = GetService(Imagefile, DiskAccess, Proxy);

            Service.StartServiceThreadAndMount(Adapter, Flags);

            return Service;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Proxy"></param>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static ReadOnlyCollection<VirtualDiskAccess> GetSupportedVirtualDiskAccess(ProxyType Proxy, string imagePath)
        {
            if (!SupportedVirtualDiskAccess.TryGetValue(Proxy, out var getSupportedVirtualDiskAccess))
                throw new ArgumentException($"Proxy type not supported: {Proxy}", nameof(Proxy));

            if (Proxy == ProxyType.DiscUtils &&
                NotSupportedFormatsForWriteOverlay.Contains(Path.GetExtension(imagePath),
                    StringComparer.OrdinalIgnoreCase))
                getSupportedVirtualDiskAccess = getSupportedVirtualDiskAccess
                    .Where(acc => acc != VirtualDiskAccess.ReadWriteOverlay).ToList().AsReadOnly();
            return getSupportedVirtualDiskAccess;
        }

        /// <summary>
        /// Creates an object, of a DiscUtils.VirtualDisk derived class, for any supported image files format.
        /// For image formats not directly supported by DiscUtils.dll, this creates a devio provider first which
        /// then is opened as a DiscUtils.VirtualDisk wrapper object so that DiscUtils virtual disk features can
        /// be used on the image anyway.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static VirtualDisk GetDiscUtilsVirtualDisk(string Imagefile, FileAccess DiskAccess, ProxyType Proxy)
        {
            VirtualDisk virtualdisk;

            switch (Proxy)
            {
                case ProxyType.DiscUtils:
                {
                    if (Imagefile.EndsWith(".ova", StringComparison.OrdinalIgnoreCase))
                        virtualdisk = OpenOVA(Imagefile, DiskAccess);
                    else
                        virtualdisk = VirtualDisk.OpenDisk(Imagefile, DiskAccess);
                    break;
                }

                default:
                {
                    var provider = GetProvider(Imagefile, DiskAccess, Proxy);
                    var geom = Geometry.FromCapacity(provider.Length, System.Convert.ToInt32(provider.SectorSize));
                    virtualdisk = new Raw.Disk(new Client.DevioDirectStream(provider, ownsProvider: true),
                        Ownership.Dispose, geom);
                    break;
                }
            }

            return virtualdisk;
        }

        /// <summary>
        /// Opens a VMDK image file embedded in an OVA archive.
        /// </summary>
        /// <param name="imagefile">Path to OVA archive file</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <returns></returns>
        public static VirtualDisk OpenOVA(string imagefile, FileAccess diskAccess)
        {
            if (diskAccess.HasFlag(FileAccess.Write))
                throw new NotSupportedException("Cannot modify OVA files");

            var ova = File.Open(imagefile, FileMode.Open, FileAccess.Read);

            try
            {
                ; /* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertInitializer(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarations(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 
                Dim vmdk = Aggregate file In Archives.TarFile.EnumerateFiles(ova)
                           Into FirstOrDefault(file.Name.EndsWith(".vmdk", StringComparison.OrdinalIgnoreCase))

 */
                if (vmdk == null)
                    throw new NotSupportedException(
                        $"The OVA file {imagefile} does not contain an embedded vmdk file.");

                Vmdk.Disk virtual_disk = new Vmdk.Disk(vmdk.GetStream(), Ownership.Dispose);
                virtual_disk.Disposed += () => ova.Dispose();
                return virtual_disk;
            }
            catch (Exception ex)
            {
                ova.Dispose();

                throw new Exception($"Error opening {imagefile}", ex);
            }
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file. This does not create a DevioServiceBase
        /// object that can actually serve incoming requests, it just creates the provider object that can
        /// be used with a later created DevioServiceBase object.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static IDevioProvider GetProvider(string Imagefile, FileAccess DiskAccess, ProxyType Proxy)
        {
            Func<string, FileAccess, IDevioProvider> GetProviderFunc = null;

            if (InstalledProvidersByProxyValueAndFileAccess.TryGetValue(Proxy, out GetProviderFunc))
                return GetProviderFunc(Imagefile, DiskAccess);

            throw new InvalidOperationException($"Proxy {Proxy} not supported.");
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file. This does not create a DevioServiceBase
        /// object that can actually serve incoming requests, it just creates the provider object that can
        /// be used with a later created DevioServiceBase object.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static IDevioProvider GetProvider(string Imagefile, VirtualDiskAccess DiskAccess, ProxyType Proxy)
        {
            uint device_number;

            if (uint.TryParse(Imagefile, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, ref device_number))
                return GetProviderPhysical(device_number, DiskAccess);
            else if ((Imagefile.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                      Imagefile.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase)) &&
                     Imagefile.IndexOf('\\', 4) < 0)
                return GetProviderPhysical(Imagefile, DiskAccess);

            Func<string, VirtualDiskAccess, IDevioProvider> GetProviderFunc = null;

            if (InstalledProvidersByProxyValueAndVirtualDiskAccess.TryGetValue(Proxy, out GetProviderFunc))
                return GetProviderFunc(Imagefile, DiskAccess);

            throw new InvalidOperationException($"Proxy {Proxy} not supported.");
        }

        public static IDevioProvider GetProvider(string Imagefile, FileAccess DiskAccess, string ProviderName)
        {
            uint device_number;

            if (uint.TryParse(Imagefile, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, ref device_number))
                return GetProviderPhysical(device_number, DiskAccess);
            else if ((Imagefile.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                      Imagefile.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase)) &&
                     Imagefile.IndexOf('\\', 4) < 0)
                return GetProviderPhysical(Imagefile, DiskAccess);

            Func<string, FileAccess, IDevioProvider> GetProviderFunc = null;

            if (InstalledProvidersByNameAndFileAccess.TryGetValue(ProviderName, out GetProviderFunc))
                return GetProviderFunc(Imagefile, DiskAccess);

            throw new NotSupportedException(
                $"Provider '{ProviderName}' not supported. Valid values are: {string.Join(", ", InstalledProvidersByNameAndFileAccess.Keys)}.");
        }

        private static DevioProviderFromStream GetProviderPhysical(uint DeviceNumber, VirtualDiskAccess DiskAccess)
        {
            return GetProviderPhysical(DeviceNumber, GetDirectFileAccessFlags(DiskAccess));
        }

        private static DevioProviderFromStream GetProviderPhysical(string DevicePath, VirtualDiskAccess DiskAccess)
        {
            return GetProviderPhysical(DevicePath, GetDirectFileAccessFlags(DiskAccess));
        }

        private static DevioProviderFromStream GetProviderPhysical(uint DeviceNumber, FileAccess DiskAccess)
        {
            using (ScsiAdapter adapter = new ScsiAdapter())
            {
                var disk = adapter.OpenDevice(DeviceNumber, DiskAccess);

                return new DevioProviderFromStream(disk.GetRawDiskStream(), ownsStream: true)
                {
                    CustomSectorSize = System.Convert.ToUInt32(disk.Geometry?.BytesPerSector ?? 512)
                };
            }
        }

        private static DevioProviderFromStream GetProviderPhysical(string DevicePath, FileAccess DiskAccess)
        {
            DiskDevice disk = new DiskDevice(DevicePath, DiskAccess);

            return new DevioProviderFromStream(disk.GetRawDiskStream(), ownsStream: true)
            {
                CustomSectorSize = System.Convert.ToUInt32(disk.Geometry?.BytesPerSector ?? 512)
            };
        }

        private static DevioProviderFromStream GetProviderRaw(string Imagefile, VirtualDiskAccess DiskAccess)
        {
            return GetProviderRaw(Imagefile, GetDirectFileAccessFlags(DiskAccess));
        }

        private static DevioProviderFromStream GetProviderRaw(string Imagefile, FileAccess DiskAccess)
        {

            var stream = NativeFileIO.OpenFileStream(Imagefile, FileMode.Open, DiskAccess,
                FileShare.Read | FileShare.Delete, Overlapped: true);
            return new DevioProviderFromStream(stream, ownsStream: true)
            {
                CustomSectorSize = GetSectorSizeFromFileName(Imagefile)
            };
        }

        public static Dictionary<ProxyType, Func<string, VirtualDiskAccess, IDevioProvider>>
            InstalledProvidersByProxyValueAndVirtualDiskAccess { get; } =
            new Dictionary<ProxyType, Func<string, VirtualDiskAccess, IDevioProvider>>()
            {
                { ProxyType.DiscUtils, GetProviderDiscUtils },
                { ProxyType.MultiPartRaw, GetProviderMultiPartRaw },
                { ProxyType.None, GetProviderRaw }
            };

        public static Dictionary<ProxyType, Func<string, FileAccess, IDevioProvider>>
            InstalledProvidersByProxyValueAndFileAccess { get; } =
            new Dictionary<ProxyType, Func<string, FileAccess, IDevioProvider>>()
            {
                { ProxyType.DiscUtils, GetProviderDiscUtils },
                { ProxyType.MultiPartRaw, GetProviderMultiPartRaw },
                { ProxyType.None, GetProviderRaw }
            };

        public static Dictionary<string, Func<string, VirtualDiskAccess, IDevioProvider>>
            InstalledProvidersByNameAndVirtualDiskAccess { get; } =
            new Dictionary<string, Func<string, VirtualDiskAccess, IDevioProvider>>(StringComparer.OrdinalIgnoreCase)
            {
                { "DiscUtils", GetProviderDiscUtils },
                { "MultiPartRaw", GetProviderMultiPartRaw },
                { "None", GetProviderRaw }
            };

        public static Dictionary<string, Func<string, FileAccess, IDevioProvider>>
            InstalledProvidersByNameAndFileAccess { get; } =
            new Dictionary<string, Func<string, FileAccess, IDevioProvider>>(StringComparer.OrdinalIgnoreCase)
            {
                { "DiscUtils", GetProviderDiscUtils },
                { "MultiPartRaw", GetProviderMultiPartRaw },
                { "None", GetProviderRaw }
            };

        private static readonly Assembly[] DiscUtilsAssemblies = new[]
        {
            typeof(Vmdk.Disk).Assembly,
            typeof(Vhdx.Disk).Assembly,
            typeof(Vhd.Disk).Assembly,
            typeof(Vdi.Disk).Assembly,
            typeof(Dmg.Disk).Assembly,
            typeof(Xva.Disk).Assembly,
            typeof(OpticalDisk.Disc).Assembly,
            typeof(Raw.Disk).Assembly
        };

        public static bool DiscUtilsInitialized { get; } = InitializeDiscUtils();

        private static bool InitializeDiscUtils()
        {
            var done = false;
            foreach (var asm in DiscUtilsAssemblies.Distinct())
            {
                Trace.WriteLine($"Registering DiscUtils assembly '{asm.FullName}'...");
                Setup.SetupHelper.RegisterAssembly(asm);
                done = true;
            }

            return done;
        }

        /// <summary>
        /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static DevioServiceBase GetService(string Imagefile, VirtualDiskAccess DiskAccess, ProxyType Proxy)
        {
            return GetService(Imagefile, DiskAccess, Proxy, FakeMBR: false);
        }

        /// <summary>
        /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static DevioServiceBase GetService(string Imagefile, VirtualDiskAccess DiskAccess, ProxyType Proxy,
            bool FakeMBR)
        {
            if (Proxy == ProxyType.None && !FakeMBR)
                return new DevioNoneService(Imagefile, DiskAccess);
            else if (Proxy == ProxyType.DiscUtils && !FakeMBR && (DiskAccess & !FileAccess.ReadWrite) == 0 &&
                     (Imagefile.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ||
                      Imagefile.EndsWith(".avhd", StringComparison.OrdinalIgnoreCase)))
                return new DevioNoneService($@"\\?\vhdaccess{NativeFileIO.GetNtPath(Imagefile)}", DiskAccess);

            var Provider = GetProvider(Imagefile, DiskAccess, Proxy);

            if (FakeMBR)
                Provider = new DevioProviderWithFakeMBR(Provider);

            var Service = new DevioShmService(Provider, OwnsProvider: true)
            {
                Description = $"Image file {Imagefile}"
            };

            return Service;
        }

        /// <summary>
        /// Creates an object, of a DevioServiceBase derived class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        /// <param name="Proxy">One of known image libraries that can handle specified image file.</param>
        public static DevioServiceBase GetService(string Imagefile, FileAccess DiskAccess, ProxyType Proxy)
        {
            DevioServiceBase Service;

            switch (Proxy)
            {
                case ProxyType.None:
                {
                    if (Imagefile.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ||
                        Imagefile.EndsWith(".avhd", StringComparison.OrdinalIgnoreCase))
                        return new DevioNoneService(@"\\?\vhdaccess" + NativeFileIO.GetNtPath(Imagefile), DiskAccess);
                    else
                        Service = new DevioNoneService(Imagefile, DiskAccess);
                    break;
                }

                default:
                {
                    Service = new DevioShmService(GetProvider(Imagefile, DiskAccess, Proxy), OwnsProvider: true);
                    break;
                }
            }

            Service.Description = $"Image file {Imagefile}";

            return Service;
        }

        internal static FileAccess GetDirectFileAccessFlags(VirtualDiskAccess DiskAccess)
        {
            if ((DiskAccess & !FileAccess.ReadWrite) != 0)
                throw new ArgumentException($"Unsupported VirtualDiskAccess flags For direct file access: {DiskAccess}",
                    nameof(DiskAccess));
            return (FileAccess)DiskAccess;
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file using DiscUtils library.
        /// </summary>
        /// <param name="Imagefile">Image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        public static IDevioProvider GetProviderDiscUtils(string Imagefile, FileAccess DiskAccess)
        {
            VirtualDiskAccess VirtualDiskAccess;

            switch (DiskAccess)
            {
                case FileAccess.Read:
                {
                    VirtualDiskAccess = VirtualDiskAccess.ReadOnly;
                    break;
                }

                case FileAccess.ReadWrite:
                {
                    VirtualDiskAccess = VirtualDiskAccess.ReadWriteOriginal;
                    break;
                }

                default:
                {
                    throw new ArgumentException($"Unsupported DiskAccess for DiscUtils: {DiskAccess}",
                        nameof(DiskAccess));
                    break;
                }
            }

            return GetProviderDiscUtils(Imagefile, VirtualDiskAccess);
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified image file using DiscUtils library.
        /// </summary>
        /// <param name="imagefile">Image file.</param>
        /// <param name="diskAccess">Read or read/write access to image file and virtual disk device.</param>
        public static IDevioProvider GetProviderDiscUtils(string imagefile, VirtualDiskAccess diskAccess)
        {
            FileAccess fileAccess;

            switch (diskAccess)
            {
                case VirtualDiskAccess.ReadOnly:
                {
                    fileAccess = FileAccess.Read;
                    break;
                }

                case VirtualDiskAccess.ReadWriteOriginal:
                {
                    fileAccess = FileAccess.ReadWrite;
                    break;
                }

                case VirtualDiskAccess.ReadWriteOverlay:
                {
                    fileAccess = FileAccess.Read;
                    break;
                }

                default:
                {
                    throw new ArgumentException($"Unsupported DiskAccess for DiscUtils: {diskAccess}",
                        nameof(diskAccess));
                }
            }

            Trace.WriteLine($"Opening image {imagefile}");

            var disk = GetDiscUtilsVirtualDisk(imagefile, fileAccess, ProxyType.DiscUtils);

            if (disk == null)
            {
                FileStream fs = new FileStream(imagefile, FileMode.Open, fileAccess, FileShare.Read | FileShare.Delete,
                    bufferSize: 1, useAsync: true);
                try
                {
                    disk = new Dmg.Disk(fs, Ownership.Dispose);
                }
                catch
                {
                    fs.Dispose();
                }
            }

            if (disk == null)
            {
                Trace.WriteLine(
                    $@"Image not recognized by DiscUtils.{Environment.NewLine} {Environment.NewLine}Formats currently supported: {string.Join(", ", VirtualDiskManager.SupportedDiskTypes)}",
                    "Error");
                return null /* TODO Change to default(_) if this is not a reference type */;
            }

            Trace.WriteLine($"Image type class: {disk.DiskTypeInfo?.Name} ({disk.DiskTypeInfo?.Variant})");

            List<IDisposable> disposableObjects = new List<IDisposable>()
            {
                disk
            };

            try
            {
                if (disk.IsPartitioned)
                    Trace.WriteLine($"Partition table class: {disk.Partitions.GetType()}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Partition table error: {ex.JoinMessages()}");
            }

            try
            {
                Trace.WriteLine($"Image virtual size is {disk.Capacity} bytes");

                uint sectorSize;

                if (disk.Geometry == null)
                {
                    sectorSize = 512;
                    Trace.WriteLine("Image sector size is unknown, assuming 512 bytes");
                }
                else
                {
                    sectorSize = System.Convert.ToUInt32(disk.Geometry.BytesPerSector);
                    Trace.WriteLine($"Image sector size is {sectorSize} bytes");
                }

                if (diskAccess == VirtualDiskAccess.ReadWriteOverlay)
                {
                    var differencingPath = Path.Combine(Path.GetDirectoryName(imagefile),
                        $"{Path.GetFileNameWithoutExtension(imagefile)}_aimdiff{Path.GetExtension(imagefile)}");

                    Trace.WriteLine($"Using temporary overlay file '{differencingPath}'");

                    do
                    {
                        try
                        {
                            if (File.Exists(differencingPath))
                            {
                                if (UseExistingDifferencingDisk(ref differencingPath))
                                {
                                    disk = VirtualDisk.OpenDisk(differencingPath, FileAccess.ReadWrite);
                                    break;
                                }

                                File.Delete(differencingPath);
                            }

                            disk = disk.CreateDifferencingDisk(differencingPath);
                            break;
                        }
                        catch (Exception ex) when (!ex.Enumerate().OfType<OperationCanceledException>().Any() &&
                                                   HandleDifferencingDiskCreationError(ex, ref differencingPath))
                        {
                        }
                    } while (true);

                    disposableObjects.Add(disk);
                }

                var diskStream = disk.Content;
                Trace.WriteLine($"Used size is {diskStream.Length} bytes");

                if (diskStream.CanWrite)
                    Trace.WriteLine("Read/write mode.");
                else
                    Trace.WriteLine("Read-only mode.");

                DevioProviderFromStream provider = new DevioProviderFromStream(diskStream, ownsStream: true)
                {
                    CustomSectorSize = sectorSize
                };

                provider.Disposed += () => disposableObjects.ForEach(obj => obj.Dispose());

                return provider;
            }
            catch (Exception ex)
            {
                disposableObjects.ForEach(obj => obj.Dispose());

                throw new Exception($"Error opening {imagefile}", ex);
            }
        }

        public class PathExceptionEventArgs : EventArgs
        {
            public Exception Exception { get; set; }

            public string Path { get; set; }

            public bool Handled { get; set; }
        }

        public static event EventHandler<PathExceptionEventArgs> DifferencingDiskCreationError;

        private static bool HandleDifferencingDiskCreationError(Exception ex, ref string differencingPath)
        {
            PathExceptionEventArgs e = new PathExceptionEventArgs()
            {
                Exception = ex,
                Path = differencingPath
            };

            DifferencingDiskCreationError?.Invoke(null, e);

            differencingPath = e.Path;

            return e.Handled;
        }

        public class PathRequestEventArgs : EventArgs
        {
            public string Path { get; set; }

            public bool Response { get; set; }
        }

        public static event EventHandler<PathRequestEventArgs> UseExistingDifferencingDiskUserRequest;

        private static bool UseExistingDifferencingDisk(ref string differencingPath)
        {
            PathRequestEventArgs e = new PathRequestEventArgs()
            {
                Path = differencingPath
            };

            UseExistingDifferencingDiskUserRequest?.Invoke(null, e);

            differencingPath = e.Path;

            return e.Response;
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified set of multi-part raw image files.
        /// </summary>
        /// <param name="Imagefile">First part image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        public static IDevioProvider GetProviderMultiPartRaw(string Imagefile, VirtualDiskAccess DiskAccess)
        {
            return GetProviderMultiPartRaw(Imagefile, GetDirectFileAccessFlags(DiskAccess));
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified set of multi-part raw image files.
        /// </summary>
        /// <param name="Imagefile">First part image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        public static IDevioProvider GetProviderMultiPartRaw(string Imagefile, FileAccess DiskAccess)
        {
            MultiPartFileStream DiskStream = new MultiPartFileStream(Imagefile, DiskAccess);

            return new DevioProviderFromStream(DiskStream, ownsStream: true)
            {
                CustomSectorSize = GetSectorSizeFromFileName(Imagefile)
            };
        }

        /// <summary>
        /// Creates an object, of a IDevioProvider implementing class, to support devio proxy server end
        /// for servicing I/O requests to a specified set of multi-part raw image files.
        /// </summary>
        /// <param name="Imagefile">First part image file.</param>
        /// <param name="DiskAccess">Read or read/write access to image file and virtual disk device.</param>
        public static IDevioProvider GetProviderMultiPartRaw(string Imagefile, FileAccess DiskAccess,
            FileShare ShareMode)
        {
            MultiPartFileStream DiskStream = new MultiPartFileStream(Imagefile, DiskAccess, ShareMode);

            return new DevioProviderFromStream(DiskStream, ownsStream: true)
            {
                CustomSectorSize = GetSectorSizeFromFileName(Imagefile)
            };
        }




        private const string ContainerIndexSeparator = ":::";

        public static VirtualDisk OpenImage(string imagepath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                (imagepath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                 imagepath.StartsWith(@"\\.\", StringComparison.Ordinal)) &&
                string.IsNullOrWhiteSpace(Path.GetExtension(imagepath)))
            {
                DiskDevice vdisk = new DiskDevice(imagepath, FileAccess.Read);
                var diskstream = vdisk.GetRawDiskStream();
                return new Raw.Disk(diskstream, Ownership.Dispose);
            }

            if (Path.GetExtension(imagepath).Equals(".001", StringComparison.Ordinal) &&
                File.Exists(Path.ChangeExtension(imagepath, ".002")))
            {
                DevioDirectStream diskstream =
                    new DevioDirectStream(GetProviderMultiPartRaw(imagepath, FileAccess.Read), ownsProvider: true);
                return new Raw.Disk(diskstream, Ownership.Dispose);
            }




            var disk = VirtualDisk.OpenDisk(imagepath, FileAccess.Read);
            if (disk == null)
                disk = new Raw.Disk(imagepath, FileAccess.Read);

            return disk;
        }

        public static Stream OpenImageAsStream(string arg)
        {
            switch (Path.GetExtension(arg).ToLowerInvariant())
            {
                case ".vhd":
                {
                    if (!DiscUtilsInitialized)
                        Trace.WriteLine("DiscUtils not available!");
                    var provider = GetProviderDiscUtils(arg, FileAccess.Read);
                    Trace.WriteLine($"Image '{arg}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }
                case ".vdi":
                {
                    if (!DiscUtilsInitialized)
                        Trace.WriteLine("DiscUtils not available!");
                    var provider = GetProviderDiscUtils(arg, FileAccess.Read);
                    Trace.WriteLine($"Image '{arg}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }
                case ".vmdk":
                {
                    if (!DiscUtilsInitialized)
                        Trace.WriteLine("DiscUtils not available!");
                    var provider = GetProviderDiscUtils(arg, FileAccess.Read);
                    Trace.WriteLine($"Image '{arg}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }
                case ".vhdx":
                {
                    if (!DiscUtilsInitialized)
                        Trace.WriteLine("DiscUtils not available!");
                    var provider = GetProviderDiscUtils(arg, FileAccess.Read);
                    Trace.WriteLine($"Image '{arg}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }
                case ".dmg":
                {
                    if (!DiscUtilsInitialized)
                        Trace.WriteLine("DiscUtils not available!");
                    var provider = GetProviderDiscUtils(arg, FileAccess.Read);
                    Trace.WriteLine($"Image '{arg}' sector size: {provider.SectorSize}");
                    return new DevioDirectStream(provider, ownsProvider: true);
                }

                case ".001":
                {
                    if (File.Exists(Path.ChangeExtension(arg, ".002")))
                        return new DevioDirectStream(GetProviderMultiPartRaw(arg, FileAccess.Read), ownsProvider: true);
                    else
                        return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                    break;
                }

                case ".raw":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                case ".dd":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                case ".img":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                case ".ima":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                case ".iso":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                case ".bin":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                case ".nrg":
                {
                    return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                }


                default:
                {
                    if ((arg.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                         arg.StartsWith(@"\\.\", StringComparison.Ordinal)))
                    {
                        DiskDevice disk = new DiskDevice(arg, FileAccess.Read);
                        var sector_size = disk.Geometry?.BytesPerSector ?? 512;
                        Console.WriteLine($"Physical disk '{arg}' sector size: {sector_size}");
                        return disk.GetRawDiskStream();
                    }
                    else
                    {
                        Console.WriteLine($"Unknown image file extension '{arg}', using raw device data.");
                        return new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }
            }
        }
    }
}
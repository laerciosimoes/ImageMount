namespace ImageMounter.Interop.Struct;

#pragma warning disable 0649
internal unsafe struct IMAGE_OPTIONAL_HEADER32
{
    //
    // Standard fields.
    //

    public readonly UInt16 Magic;
    public readonly Byte MajorLinkerVersion;
    public readonly Byte MinorLinkerVersion;
    public readonly UInt32 SizeOfCode;
    public readonly UInt32 SizeOfInitializedData;
    public readonly UInt32 SizeOfUninitializedData;
    public readonly UInt32 AddressOfEntryPoint;
    public readonly UInt32 BaseOfCode;
    public readonly UInt32 BaseOfData;

    //
    // NT additional fields.
    //

    public readonly UInt32 ImageBase;
    public readonly UInt32 SectionAlignment;
    public readonly UInt32 FileAlignment;
    public readonly UInt16 MajorOperatingSystemVersion;
    public readonly UInt16 MinorOperatingSystemVersion;
    public readonly UInt16 MajorImageVersion;
    public readonly UInt16 MinorImageVersion;
    public readonly UInt16 MajorSubsystemVersion;
    public readonly UInt16 MinorSubsystemVersion;
    public readonly UInt32 Win32VersionValue;
    public readonly UInt32 SizeOfImage;
    public readonly UInt32 SizeOfHeaders;
    public readonly UInt32 CheckSum;
    public readonly UInt16 Subsystem;
    public readonly UInt16 DllCharacteristics;
    public readonly UInt32 SizeOfStackReserve;
    public readonly UInt32 SizeOfStackCommit;
    public readonly UInt32 SizeOfHeapReserve;
    public readonly UInt32 SizeOfHeapCommit;
    public readonly UInt32 LoaderFlags;
    public readonly UInt32 NumberOfRvaAndSizes;
    public fixed UInt32 DataDirectory[32];
}
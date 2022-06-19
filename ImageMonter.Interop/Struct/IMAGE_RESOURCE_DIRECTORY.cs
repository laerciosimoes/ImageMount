namespace ImageMounter.Interop.Struct;

#pragma warning disable 0649
internal struct IMAGE_RESOURCE_DIRECTORY
{
    public readonly UInt32 Characteristics;
    public readonly UInt32 TimeDateStamp;
    public readonly UInt16 MajorVersion;
    public readonly UInt16 MinorVersion;
    public readonly UInt16 NumberOfNamedEntries;
    public readonly UInt16 NumberOfIdEntries;
    //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
}
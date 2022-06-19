namespace ImageMounter.Interop.Struct;

#pragma warning disable 0649
internal struct IMAGE_RESOURCE_DIRECTORY_ENTRY
{
    readonly UInt32 NameId;
    public uint OffsetToData { get; }

    public bool NameIsString => (NameId & 0x80000000) != 0;
    public ushort Id => (ushort)NameId;
    public uint NameOffset => NameId & 0x7fffffffu;
    public bool DataIsDirectory => (OffsetToData & 0x80000000u) != 0;
    public uint OffsetToDirectory => OffsetToData & 0x7fffffffu;
}
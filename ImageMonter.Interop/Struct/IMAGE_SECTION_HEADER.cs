namespace ImageMounter.Interop.Struct;

#pragma warning disable 0649
internal struct IMAGE_SECTION_HEADER
{
    public readonly long Name;
    public readonly UInt32 VirtualSize;
    public readonly UInt32 VirtualAddress;
    public readonly UInt32 SizeOfRawData;
    public readonly UInt32 PointerToRawData;
    public readonly UInt32 PointerToRelocations;
    public readonly UInt32 PointerToLinenumbers;
    public readonly UInt16 NumberOfRelocations;
    public readonly UInt16 NumberOfLinenumbers;
    public readonly UInt32 Characteristics;
}
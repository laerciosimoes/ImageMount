using DWORD = System.UInt32;

#pragma warning disable 0649

namespace ImageMounter.Interop.Struct;

internal struct IMAGE_RESOURCE_DATA_ENTRY
{
    public readonly DWORD OffsetToData;
    public readonly DWORD Size;
    public readonly DWORD CodePage;
    public readonly DWORD Reserved;
}


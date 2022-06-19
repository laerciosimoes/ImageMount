using System.Runtime.InteropServices;

namespace ImageMounter.Interop.Struct;

/// <summary>
/// Structure for counted Unicode strings used in NT API calls
/// </summary>
public struct UNICODE_STRING
{
    /// <summary>
    /// Length in bytes of Unicode string pointed to by Buffer
    /// </summary>
    public ushort Length { get; }

    /// <summary>
    /// Maximum length in bytes of string memory pointed to by Buffer
    /// </summary>
    public ushort MaximumLength { get; }

    /// <summary>
    /// Unicode character buffer in unmanaged memory
    /// </summary>
    public IntPtr Buffer { get; }

    /// <summary>
    /// Initialize with pointer to existing unmanaged string
    /// </summary>
    /// <param name="str">Pointer to existing unicode string in managed memory</param>
    /// <param name="byteCount">Length in bytes of string pointed to by <paramref name="str"/></param>
    public UNICODE_STRING(IntPtr str, ushort byteCount)
    {
        Length = byteCount;
        MaximumLength = byteCount;
        Buffer = str;
    }

    /// <summary>
    /// Creates a managed string object from UNICODE_STRING instance.
    /// </summary>
    /// <returns>Managed string</returns>
    public override string ToString()
    {
        return Length == 0 ? string.Empty : Marshal.PtrToStringUni(Buffer, Length >> 1);
    }
}

namespace ImageMounter.IO.Native.Enum
{
    [Flags]
    public enum NtObjectAttributes
    {
        Inherit = 0x2,
        Permanent = 0x10,
        Exclusive = 0x20,
        CaseInsensitive = 0x40,
        OpenIf = 0x80,
        OpenLink = 0x100
    }
}

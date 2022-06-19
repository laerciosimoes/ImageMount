namespace ImageMounter.IO.Native.Enum
{
    [Flags]
    public enum ShutdownReasons : UInt32
    {
        ReasonFlagPlanned = 0x80000000U
    }
}

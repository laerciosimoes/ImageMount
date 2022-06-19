namespace ImageMounter.IO.Native.Enum
{
    [Flags]
    public enum DEFINE_DOS_DEVICE_FLAGS : UInt32
    {
        DDD_EXACT_MATCH_ON_REMOVE = 0x4,
        DDD_NO_BROADCAST_SYSTEM = 0x8,
        DDD_RAW_TARGET_PATH = 0x1,
        DDD_REMOVE_DEFINITION = 0x2
    }
}

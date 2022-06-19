namespace ImageMounter.IO.Native.Enum
{

    [Flags]
    public enum ShutdownFlags : UInt32
    {
        HybridShutdown = 0x400000U,
        Logoff = 0x0U,
        PowerOff = 0x8U,
        Reboot = 0x2U,
        RestartApps = 0x40U,
        Shutdown = 0x1U,
        Force = 0x4U,
        ForceIfHung = 0x10U
    }
}

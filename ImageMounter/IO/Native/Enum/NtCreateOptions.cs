namespace ImageMounter.IO.Native.Enum
{

    [Flags]
    public enum NtCreateOptions
    {
        DirectoryFile = 0x1,
        WriteThrough = 0x2,
        SequentialOnly = 0x4,
        NoIntermediateBuffering = 0x8,
        SynchronousIoAlert = 0x10,
        SynchronousIoNonAlert = 0x20,
        NonDirectoryFile = 0x40,
        CreateTreeConnection = 0x80,
        CompleteIfOpLocked = 0x100,
        NoEAKnowledge = 0x200,
        OpenForRecovery = 0x400,
        RandomAccess = 0x800,
        DeleteOnClose = 0x1000,
        OpenByFileId = 0x200,
        OpenForBackupIntent = 0x400,
        NoCompression = 0x8000,
        ReserverNoOpFilter = 0x100000,
        OpenReparsePoint = 0x200000,
        OpenNoRecall = 0x400000
    }

}

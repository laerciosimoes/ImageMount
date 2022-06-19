namespace ImageMounter.IO.Native.Struct
{
    public enum Win32FileType : Int32
    {
        Unknown = 0x0,
        Disk = 0x1,
        Character = 0x2,
        Pipe = 0x3,
        Remote = 0x8000
    }

}

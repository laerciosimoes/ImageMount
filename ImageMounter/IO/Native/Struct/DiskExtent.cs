namespace ImageMounter.IO.Native.Struct
{
    public struct DiskExtent
    {
        public uint DiskNumber { get; }
        public long StartingOffset { get; }
        public long ExtentLength { get; }
    }
}

using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FindStreamData
    {
        public Int64 StreamSize { get; }

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        private readonly string _streamName;

        public string NamePart => _streamName?.Split(':').ElementAtOrDefault(1);

        public string TypePart => _streamName?.Split(':').ElementAtOrDefault(2);

        public string StreamName => _streamName;
    }

}

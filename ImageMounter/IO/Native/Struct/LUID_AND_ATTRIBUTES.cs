using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LUID_AND_ATTRIBUTES
    {
        public long LUID { get; set; }
        public int Attributes { get; set; }

        public override string ToString()
        {
            return $"LUID = 0x{LUID}, Attributes = 0x{Attributes}";
        }
    }

}

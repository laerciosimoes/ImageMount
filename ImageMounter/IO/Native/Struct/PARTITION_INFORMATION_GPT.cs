using System.Runtime.InteropServices;
using ImageMounter.Interop;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PARTITION_INFORMATION_GPT
    {
        public Guid PartitionType { get; }

        public Guid PartitionId { get; }

        public GptAttributes Attributes { get; }

        private readonly long _name0;
        private readonly long _name1;
        private readonly long _name2;
        private readonly long _name3;
        private readonly long _name4;
        private readonly long _name5;
        private readonly long _name6;
        private readonly long _name7;
        private readonly long _name8;

        public string Name
        {
            get
            {
                using (PinnedBuffer<char> buffer = new PinnedBuffer<char>(56))
                {
                    buffer.Write(0, this);
                    return new string(buffer.Target, 20, 36);
                }
            }
        }
    }

}

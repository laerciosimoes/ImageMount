using System.Runtime.InteropServices;
using ImageMounter.IO.Native.Enum;

namespace ImageMounter.IO.Native.Struct
{

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_PROPERTY_QUERY
    {
        public STORAGE_PROPERTY_ID PropertyId { get; }

        public STORAGE_QUERY_TYPE QueryType { get; }

        private readonly byte _additional;

        public STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID PropertyId, STORAGE_QUERY_TYPE QueryType)
        {
            PropertyId = PropertyId;
            QueryType = QueryType;
        }
    }

}

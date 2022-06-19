using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.IO.Native.Struct;

namespace ImageMounter.IO
{
    public sealed class HandleTableEntryInformation
    {
        public SystemHandleTableEntryInformation HandleTableEntry { get; }

        public string ObjectType { get; }

        public string ObjectName { get; }
        public string ProcessName { get; }
        public DateTime ProcessStartTime { get; }
        public int SessionId { get; }

        internal HandleTableEntryInformation(ref SystemHandleTableEntryInformation HandleTableEntry,string ObjectType, string ObjectName, Process Process)
        {
            HandleTableEntry = HandleTableEntry;
            ObjectType = ObjectType;
            ObjectName = ObjectName;
            ProcessName = Process.ProcessName;
            ProcessStartTime = Process.StartTime;
            SessionId = Process.SessionId;
        }
    }

}

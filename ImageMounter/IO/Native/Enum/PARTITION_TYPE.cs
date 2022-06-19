using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.IO.Native.Enum
{
    [Flags]
    public enum PARTITION_TYPE : byte
    {
        PARTITION_ENTRY_UNUSED = 0x0      // Entry unused
        ,
        PARTITION_FAT_12 = 0x1      // 12-bit FAT entries
        ,
        PARTITION_XENIX_1 = 0x2      // Xenix
        ,
        PARTITION_XENIX_2 = 0x3      // Xenix
        ,
        PARTITION_FAT_16 = 0x4      // 16-bit FAT entries
        ,
        PARTITION_EXTENDED = 0x5      // Extended partition entry
        ,
        PARTITION_HUGE = 0x6      // Huge partition MS-DOS V4
        ,
        PARTITION_IFS = 0x7      // IFS Partition
        ,
        PARTITION_OS2BOOTMGR = 0xA      // OS/2 Boot Manager/OPUS/Coherent swap
        ,
        PARTITION_FAT32 = 0xB      // FAT32
        ,
        PARTITION_FAT32_XINT13 = &H      // FAT32 using extended int13 services
        ,
        PARTITION_XINT13 = 0xE      // Win95 partition using extended int13 services
        ,
        PARTITION_XINT13_EXTENDED = 0xF      // Same as type 5 but uses extended int13 services
        ,
        PARTITION_PREP = 0x41      // PowerPC Reference Platform (PReP) Boot Partition
        ,
        PARTITION_LDM = 0x42      // Logical Disk Manager partition
        ,
        PARTITION_UNIX = 0x63      // Unix
        ,
        PARTITION_NTFT = 0x80      // NTFT partition      
    }
}

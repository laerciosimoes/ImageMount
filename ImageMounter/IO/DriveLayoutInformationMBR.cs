using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.IO.Native.Struct;

namespace ImageMounter.IO
{

    public class DriveLayoutInformationMBR : DriveLayoutInformation
    {
        public DRIVE_LAYOUT_INFORMATION_MBR MBR { get; }

        public DriveLayoutInformationMBR(DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation,
            PARTITION_INFORMATION_EX[] Partitions,
            DRIVE_LAYOUT_INFORMATION_MBR DriveLayoutInformationMBR) : base(DriveLayoutInformation,
            Partitions)
        {
            MBR = DriveLayoutInformationMBR;
        }

        public override int GetHashCode()
        {
            return MBR.GetHashCode();
        }

        public override string ToString()
        {
            return MBR.ToString();
        }
    }

}

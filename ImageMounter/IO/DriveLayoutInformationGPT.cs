using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.IO.Native.Struct;

namespace ImageMounter.IO
{
    public class DriveLayoutInformationGPT : DriveLayoutInformation
    {
        public DRIVE_LAYOUT_INFORMATION_GPT GPT { get; }

        public DriveLayoutInformationGPT(DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation,
            PARTITION_INFORMATION_EX[] Partitions,
            DRIVE_LAYOUT_INFORMATION_GPT DriveLayoutInformationGPT) : base(DriveLayoutInformation,
            Partitions)
        {
            GPT = DriveLayoutInformationGPT;
        }

        public override int GetHashCode()
        {
            return GPT.GetHashCode();
        }

        public override string ToString()
        {
            return GPT.ToString();
        }
    }

}

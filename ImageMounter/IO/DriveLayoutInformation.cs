using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.IO.Native.Struct;

namespace ImageMounter.IO
{
    public class DriveLayoutInformation
    {
        public DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation { get; }

        public ReadOnlyCollection<PARTITION_INFORMATION_EX> Partitions { get; }

        public DriveLayoutInformation(DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation,
            IList<PARTITION_INFORMATION_EX> Partitions)
        {
            DriveLayoutInformation = DriveLayoutInformation;
            Partitions = new ReadOnlyCollection<PARTITION_INFORMATION_EX>(Partitions);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return "N/A";
        }
    }

}

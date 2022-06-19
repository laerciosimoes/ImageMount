using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SCSI_ADDRESS : IEquatable<SCSI_ADDRESS>
    {
        public UInt32 Length { get; }
        public byte PortNumber { get; }
        public byte PathId { get; }
        public byte TargetId { get; }
        public byte Lun { get; }

        public SCSI_ADDRESS(byte PortNumber, UInt32 DWordDeviceNumber)
        {
            this.Length = System.Convert.ToUInt32(Marshal.SizeOf(this));
            this.PortNumber = PortNumber;
            this.DWordDeviceNumber = DWordDeviceNumber;
        }

        public SCSI_ADDRESS(UInt32 DWordDeviceNumber)
        {
            this.Length = System.Convert.ToUInt32(Marshal.SizeOf(this));
            this.DWordDeviceNumber = DWordDeviceNumber;
        }

        public UInt32 DWordDeviceNumber
        {
            get
            {
                return System.Convert.ToUInt32(PathId) | (System.Convert.ToUInt32(TargetId) << 8) | (System.Convert.ToUInt32(Lun) << 16);
            }
            set
            {
                PathId = System.Convert.ToByte(Value & 0xFF);
                TargetId = System.Convert.ToByte((Value >> 8) & 0xFF);
                Lun = System.Convert.ToByte((Value >> 16) & 0xFF);
            }
        }

        public override string ToString()
        {
            return $"Port = {PortNumber}, Path = {PathId}, Target = {TargetId}, Lun = {Lun}";
        }

        public new bool Equals(SCSI_ADDRESS other)
        {
            return PortNumber.Equals(other.PortNumber) && PathId.Equals(other.PathId) && TargetId.Equals(other.TargetId) && Lun.Equals(other.Lun);
        }

        public override bool Equals(object obj)
        {
            if (!obj is SCSI_ADDRESS)
                return false;

            return Equals((SCSI_ADDRESS)obj);
        }

        public override int GetHashCode()
        {
            return System.Convert.ToInt32(PathId) | (System.Convert.ToInt32(TargetId) << 8) | (System.Convert.ToInt32(Lun) << 16);
        }

        public static bool operator ==(SCSI_ADDRESS first, SCSI_ADDRESS second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(SCSI_ADDRESS first, SCSI_ADDRESS second)
        {
            return !first.Equals(second);
        }
    }

}

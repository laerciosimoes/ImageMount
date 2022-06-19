namespace ImageMounter.IO.Native.Struct
{

    public struct ScsiAddressAndLength : IEquatable<ScsiAddressAndLength>
    {
        public SCSI_ADDRESS ScsiAddress { get; }

        public long Length { get; }

        public ScsiAddressAndLength(SCSI_ADDRESS ScsiAddress, long Length)
        {
            ScsiAddress = ScsiAddress;
            Length = Length;
        }

        public override bool Equals(object obj)
        {
            if (!obj is ScsiAddressAndLength)
                return false;

            return Equals((ScsiAddressAndLength)obj);
        }

        public override int GetHashCode()
        {
            return ScsiAddress.GetHashCode() ^ Length.GetHashCode();
        }

        public override string ToString()
        {
            return $"{ScsiAddress}, Length = {Length}";
        }

        public new bool Equals(ScsiAddressAndLength other)
        {
            return Length.Equals(other.Length) && ScsiAddress.Equals(other.ScsiAddress);
        }

        public static bool operator ==(ScsiAddressAndLength a, ScsiAddressAndLength b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(ScsiAddressAndLength a, ScsiAddressAndLength b)
        {
            return a.Equals(b);
        }
    }


}

namespace ImageMounter.IO.Native.Struct
{
    public struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader { get; set; }
        public UInt32 StateChange { get; set; }
        public UInt32 Scope { get; set; }
        public UInt32 HwProfile { get; set; }
    }
}

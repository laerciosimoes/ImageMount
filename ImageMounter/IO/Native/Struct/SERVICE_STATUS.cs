using System.Runtime.InteropServices;

namespace ImageMounter.IO.Native.Struct
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS
    {
        public int ServiceType { get; }
        public int CurrentState { get; }
        public int ControlsAccepted { get; }
        public int Win32ExitCode { get; }
        public int ServiceSpecificExitCode { get; }
        public int CheckPoint { get; }
        public int WaitHint { get; }
    }
}

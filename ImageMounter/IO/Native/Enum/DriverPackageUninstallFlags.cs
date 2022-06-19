namespace ImageMounter.IO.Native.Enum
{
    [Flags]
    public enum DriverPackageUninstallFlags
    {
        Normal = 0x0,
        DeleteFiles = NativeConstants.DRIVER_PACKAGE_DELETE_FILES,
        Force = NativeConstants.DRIVER_PACKAGE_FORCE,
        Silent = NativeConstants.DRIVER_PACKAGE_SILENT
    }
}

namespace ImageMounter.IO.Native.Enum
{
    public enum CmDevNodeRegistryProperty : UInt32
    {
        CM_DRP_DEVICEDESC = 0x1,
        CM_DRP_HARDWAREID = 0x2,
        CM_DRP_COMPATIBLEIDS = 0x3,
        CM_DRP_SERVICE = 0x5,
        CM_DRP_CLASS = 0x8,
        CM_DRP_CLASSGUID = 0x9,
        CM_DRP_DRIVER = 0xA,
        CM_DRP_MFG = 0xC,
        CM_DRP_FRIENDLYNAME = 0xD,
        CM_DRP_LOCATION_INFORMATION = 0xE,
        CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME = 0xF,
        CM_DRP_UPPERFILTERS = 0x12,
        CM_DRP_LOWERFILTERS = 0x13
    }
}

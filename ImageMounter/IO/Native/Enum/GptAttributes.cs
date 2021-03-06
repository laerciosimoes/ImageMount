namespace ImageMounter.IO.Native.Enum
{
    [Flags]
    public enum GptAttributes : long
    {
        GPT_ATTRIBUTE_PLATFORM_REQUIRED = 0x1,
        GPT_BASIC_DATA_ATTRIBUTE_NO_DRIVE_LETTER = 0x8000000000000000,
        GPT_BASIC_DATA_ATTRIBUTE_HIDDEN = 0x4000000000000000,
        GPT_BASIC_DATA_ATTRIBUTE_SHADOW_COPY = 0x2000000000000000,
        GPT_BASIC_DATA_ATTRIBUTE_READ_ONLY = 0x1000000000000000
    }
}

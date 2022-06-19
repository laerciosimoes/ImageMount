namespace ImageMounter.IO.Native.Enum
{

    public enum STORAGE_QUERY_TYPE
    {
        PropertyStandardQuery = 0          // ' Retrieves the descriptor
        ,
        PropertyExistsQuery                // ' Used To test whether the descriptor Is supported
        ,
        PropertyMaskQuery                  // ' Used To retrieve a mask Of writable fields In the descriptor
        ,
        PropertyQueryMaxDefined            // ' use To validate the value
    }
}

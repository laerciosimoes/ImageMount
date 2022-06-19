using System.Runtime.InteropServices;
using ImageMounter.Interop.Struct;

namespace ImageMounter.Interop
{
    /// <summary>
    /// Pins a managed string for unmanaged use.
    /// </summary>
    [ComVisible(false)]
    public class PinnedString : PinnedBuffer
    {
        /// <summary>
        /// Returns managed object pinned by this instance.
        /// </summary>
        public new string Target => (string)GCHandle.Target;

        /// <summary>
        /// Creates a UNICODE_STRING structure pointing to the string buffer
        /// pinned by this instance. Useful for calls into ntdll.dll, LSA and
        /// similar native operating system components.
        /// </summary>
        public UNICODE_STRING UnicodeString => new(handle, checked((ushort)ByteLength));

        /// <summary>
        /// Initializes a new instance with an existing managed string and pins memory
        /// position.
        /// </summary>
        /// <param name="str">Managed string to pin in unmanaged memory.</param>
        public PinnedString(string str)
            : base(str, str.Length << 1)
        {
        }

        /// <summary>
        /// Initializes a new instance with a new managed string and pins memory position.
        /// </summary>
        /// <param name="count">Size in characters of managed string to pin in unmanaged memory.</param>
        public PinnedString(int count)
            : this(new string('\0', count))
        {
        }
    }

}

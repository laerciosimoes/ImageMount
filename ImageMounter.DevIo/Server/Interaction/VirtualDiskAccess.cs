using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.DevIo.Server.Interaction
{
    /// <summary>
    /// Virtual disk access modes. A list of supported modes for a particular ProxyType
    /// is obtained by calling GetSupportedVirtualDiskAccess().
    /// </summary>
    public enum VirtualDiskAccess
    {
        ReadOnly = 1,
        ReadWriteOriginal = 3,
        ReadWriteOverlay = 7,
        ReadOnlyFileSystem = 9,
        ReadWriteFileSystem = 11
    }
}

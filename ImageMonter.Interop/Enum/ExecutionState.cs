using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.Interop.Enum
{
    /// <summary>
    /// Flags indicating what system resource and interface are required
    /// </summary>
    [Flags]
    public enum ExecutionState : uint
    {
        /// <summary>
        /// </summary>
        SystemRequired = 0x00000001,
        /// <summary>
        /// </summary>
        DisplayRequired = 0x00000002,
        /// <summary>
        /// </summary>
        UserPresent = 0x00000004,
        /// <summary>
        /// </summary>
        AwaymodeRequired = 0x00000040,
        /// <summary>
        /// </summary>
        Continuous = 0x80000000
    }
}

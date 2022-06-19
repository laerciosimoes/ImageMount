using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMounter.DevIo.Server.Services;

namespace ImageMountTool
{
    internal class ServiceListItem
    {
        public string ImageFile { get; set; }
        public DevioServiceBase Service { get; set; }
    }
}

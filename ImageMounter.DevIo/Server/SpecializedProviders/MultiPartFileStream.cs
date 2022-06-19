// '''' MultiPartFileStream.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.IO;

namespace Server.SpecializedProviders
{
    public class MultiPartFileStream : CombinedSeekStream
    {
        public MultiPartFileStream(string[] Imagefiles, FileAccess DiskAccess) : this(Imagefiles, DiskAccess, FileShare.Read | FileShare.Delete)
        {
        }

        public MultiPartFileStream(string[] Imagefiles, FileAccess DiskAccess, FileShare ShareMode) : base(DiskAccess.HasFlag(FileAccess.Write), OpenImagefiles(Imagefiles, DiskAccess, ShareMode))
        {
        }

        private static FileStream[] OpenImagefiles(string[] Imagefiles, FileAccess DiskAccess, FileShare ShareMode)
        {
            if (Imagefiles == null)
                throw new ArgumentNullException(nameof(Imagefiles));
            if (Imagefiles.Length == 0)
                throw new ArgumentException("No image file names provided.", nameof(Imagefiles));

            FileStream[] imagestreams = null;

            try
            {
                imagestreams = Array.ConvertAll(Imagefiles, Imagefile =>
                {
                    Trace.WriteLine($"Opening image {Imagefile}");
                    return new FileStream(Imagefile, FileMode.Open, DiskAccess, ShareMode);
                });
            }
            catch (Exception ex)
            {
                if (imagestreams != null)
                    Array.ForEach(imagestreams, imagestream => imagestream.Close());

                throw new IOException($"Error opening image files '{Imagefiles.FirstOrDefault()}'", ex);
            }

            return imagestreams;
        }

        public MultiPartFileStream(string FirstImagefile, FileAccess DiskAccess) : this(GetMultiSegmentFiles(FirstImagefile), DiskAccess)
        {
        }

        public MultiPartFileStream(string FirstImagefile, FileAccess DiskAccess, FileShare ShareMode) : this(GetMultiSegmentFiles(FirstImagefile), DiskAccess, ShareMode)
        {
        }
    }
}

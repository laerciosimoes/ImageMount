using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter.DevIo.Server.GenericProviders;

/// <summary>
///  Class that implements <see>IDevioProvider</see> interface with a System.IO.Stream
///  object as storage backend.
///  </summary>
public class DevioProviderFromStream : DevioProviderManagedBase
{
    /* TODO ERROR: Skipped EndIfDirectiveTrivia */
    /// <summary>
    /// Stream object used by this instance.
    /// </summary>
    public Stream BaseStream { get; }

    /// <summary>
    /// Indicates whether base stream will be automatically closed when this
    /// instance is disposed.
    /// </summary>
    public bool OwnsBaseStream { get; }

    /// <summary>
    /// Creates an object implementing IDevioProvider interface with I/O redirected
    /// to an object of a class derived from System.IO.Stream.
    /// </summary>
    /// <param name="Stream">Object of a class derived from System.IO.Stream.</param>
    /// <param name="ownsStream">Indicates whether Stream object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioProviderFromStream(Stream Stream, bool ownsStream)
    {
        BaseStream = Stream;
        OwnsBaseStream = ownsStream;
    }

    /// <summary>
    /// Returns value of BaseStream.CanWrite.
    /// </summary>
    /// <value>Value of BaseStream.CanWrite.</value>
    /// <returns>Value of BaseStream.CanWrite.</returns>
    public override bool CanWrite
    {
        get
        {
            return BaseStream.CanWrite;
        }
    }

    /// <summary>
    /// Returns value of BaseStream.Length.
    /// </summary>
    /// <value>Value of BaseStream.Length.</value>
    /// <returns>Value of BaseStream.Length.</returns>
    public override long Length
    {
        get
        {
            return BaseStream.Length;
        }
    }

    /// <summary>
    /// Returns a fixed value of 512.
    /// </summary>
    /// <value>512</value>
    /// <returns>512</returns>
    public uint CustomSectorSize { get; set; } = 512;

    /// <summary>
    /// Returns a fixed value of 512.
    /// </summary>
    /// <value>512</value>
    /// <returns>512</returns>
    public override uint SectorSize
    {
        get
        {
            return CustomSectorSize;
        }
    }

    /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
    public new override int Read(byte[] buffer, int bufferoffset, int count, long fileoffset)
    {
        BaseStream.Position = fileoffset;

        if (BaseStream.Position <= BaseStream.Length && count > BaseStream.Length - BaseStream.Position)
            count = System.Convert.ToInt32(BaseStream.Length - BaseStream.Position);

        return BaseStream.Read(buffer, bufferoffset, count);
    }

    public new override int Write(byte[] buffer, int bufferoffset, int count, long fileoffset)
    {
        BaseStream.Position = fileoffset;
        BaseStream.Write(buffer, bufferoffset, count);
        return count;
    }
    /* TODO ERROR: Skipped EndIfDirectiveTrivia */
    protected override void Dispose(bool disposing)
    {
        if (disposing && OwnsBaseStream && BaseStream != null)
            BaseStream.Dispose();

        BaseStream = null;

        base.Dispose(disposing);
    }
}
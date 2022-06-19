using System.IO;
using System.Text;

namespace ImageMounter.IO
{

    /// <summary>
    /// Buffered version of the BinaryWriter class. Writes to a MemoryStream internally and flushes
    /// writes out contents of MemoryStream when WriteTo() or ToArray() are called.
    /// </summary>
    public class BufferedBinaryWriter : BinaryWriter
    {

        /// <summary>
        ///     Creates a new instance of BufferedBinaryWriter.
        ///     </summary>
        ///     <param name="encoding">Specifies which text encoding to use.</param>
        public BufferedBinaryWriter(Encoding encoding) : base(new MemoryStream(), encoding)
        {
        }

        /// <summary>
        ///     Creates a new instance of BufferedBinaryWriter using System.Text.Encoding.Unicode text encoding.
        ///     </summary>
        public BufferedBinaryWriter() : base(new MemoryStream(), Encoding.Unicode)
        {
        }

        /// <summary>
        ///     Writes current contents of internal MemoryStream to another stream and resets
        ///     this BufferedBinaryWriter to empty state.
        ///     </summary>
        ///     <param name="stream"></param>
        public void WriteTo(Stream stream)
        {
            BinaryWriter.Flush();
            {
                var withBlock = (MemoryStream)BinaryWriter.BaseStream;
                withBlock.WriteTo(stream);
                withBlock.SetLength(0);
                withBlock.Position = 0;
            }
            stream.Flush();
        }

        /// <summary>
        ///     Extracts current contents of internal MemoryStream to a new byte array and resets
        ///     this BufferedBinaryWriter to empty state.
        ///     </summary>
        public byte[] ToArray()
        {
            BinaryWriter.Flush();
            {
                var withBlock = (MemoryStream)BinaryWriter.BaseStream;
                ToArray = withBlock.ToArray();
                withBlock.SetLength(0);
                withBlock.Position = 0;
            }
        }

        /// <summary>
        ///     Clears contents of internal MemoryStream.
        ///     </summary>
        public void Clear()
        {
            if (IsDisposed == true)
                return;

            {
                var withBlock = (MemoryStream)BinaryWriter.BaseStream;
                withBlock.SetLength(0);
                withBlock.Position = 0;
            }
        }

        public bool IsDisposed { get; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;

            base.Dispose(disposing);
        }
    }
}

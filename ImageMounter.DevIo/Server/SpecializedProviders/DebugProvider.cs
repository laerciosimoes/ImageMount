using System.Diagnostics;
using System.Runtime.InteropServices;
using ImageMounter.Devio.Interop;
using ImageMounter.Devio.Interop.Server.GenericProviders;
using Server.GenericProviders;

namespace Server.SpecializedProviders
{

    /// <summary>
    /// A class to support test cases to verify that correct data is received through providers
    /// compared to raw image files.
    /// </summary>
    public class DebugProvider : DevioProviderUnmanagedBase
    {
        public IDevioProvider BaseProvider { get; }

        public Stream DebugCompareStream { get; }

        public DebugProvider(IDevioProvider BaseProvider, Stream DebugCompareStream)
        {
            if (BaseProvider == null)
                throw new ArgumentNullException(nameof(BaseProvider));

            if (DebugCompareStream == null)
                throw new ArgumentNullException(nameof(DebugCompareStream));

            if ((!DebugCompareStream.CanSeek) || (!DebugCompareStream.CanRead))
                throw new ArgumentException("Debug compare stream must support seek and read operations.", nameof(DebugCompareStream));

            this.BaseProvider = BaseProvider;
            this.DebugCompareStream = DebugCompareStream;
        }

        public override bool CanWrite
        {
            get
            {
                return BaseProvider.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return BaseProvider.Length;
            }
        }

        public override uint SectorSize
        {
            get
            {
                return BaseProvider.SectorSize;
            }
        }

        [System.Runtime.InteropServices.DllImport("msvcrt")]
        private static extern int memcmp([In] IntPtr buf1, byte[] buf2, IntPtr count);

        public override int Read(IntPtr buf1, int bufferoffset, int count, long fileoffset)
        {
            ;/* Cannot convert LocalDeclarationStatementSyntax, System.NotSupportedException: StaticKeyword not supported!
   at ICSharpCode.CodeConverter.CSharp.SyntaxKindExtensions.ConvertToken(SyntaxKind t, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifier(SyntaxToken m, TokenContext context)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertModifiersCore>d__15.MoveNext()
   at System.Linq.Enumerable.WhereEnumerableIterator`1.MoveNext()
   at Microsoft.CodeAnalysis.SyntaxTokenList.CreateNode(IEnumerable`1 tokens)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertModifiers(IEnumerable`1 modifiers, TokenContext context, Boolean isVariableOrConst)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

            Static buf2 As Byte()

 */
            if (buf2 == null || buf2.Length < count)
                Array.Resize(ref buf2, count);
            DebugCompareStream.Position = fileoffset;
            var compareTask = DebugCompareStream.ReadAsync(buf2, 0, count);

            var rc1 = BaseProvider.Read(buf1, bufferoffset, count, fileoffset);
            var rc2 = compareTask.Result;

            if (rc1 != rc2)
                Trace.WriteLine($"Read request at position 0x{fileoffset}, 0x{count} bytes, returned 0x{rc1} bytes from image provider and 0x{rc2} bytes from debug compare stream.");

            if (memcmp(buf1 + bufferoffset, buf2, new IntPtr(Math.Min(rc1, rc2))) != 0)
                Trace.WriteLine($"Read request at position 0x{fileoffset}, 0x{count} bytes, returned different data from image provider than from debug compare stream.");

            return rc1;
        }

        public override int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
        {
            return BaseProvider.Write(buffer, bufferoffset, count, fileoffset);
        }

        public override bool SupportsShared
        {
            get
            {
                return BaseProvider.SupportsShared;
            }
        }

        public override void SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
        {
            BaseProvider.SharedKeys(Request, Response, Keys);
        }

        protected override void OnDisposed(EventArgs e)
        {
            BaseProvider.Dispose();
            DebugCompareStream.Close();

            base.OnDisposed(e);
        }
    }
}

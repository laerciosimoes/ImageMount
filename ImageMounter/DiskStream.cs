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
using System.Runtime.Versioning;
using ImageMounter.Interop.IO;
using ImageMounter.IO;
using Microsoft.Win32.SafeHandles;


namespace ImageMounter
{
    
    /// <summary>
    /// A FileStream derived class that represents disk devices by overriding properties and methods
    /// where FileStream base implementation rely on file API not directly compatible with disk device objects.
    /// </summary>
    public class DiskStream : AligningStream
    {

        /// <summary>
        /// Initializes an DiskStream object for an open disk device.
        /// </summary>
        /// <param name="SafeFileHandle">Open file handle for disk device.</param>
        /// <param name="AccessMode">Access to request for stream.</param>
        protected internal DiskStream(SafeFileHandle SafeFileHandle, FileAccess AccessMode) : base(new FileStream(SafeFileHandle, AccessMode, bufferSize: 1), Alignment: NativeFileIO.GetDiskGeometry(SafeFileHandle)?.BytesPerSector ?? 512, ownsBaseStream: true)
        {
        }

        private long? _CachedLength;

        /// <summary>
        /// Initializes an DiskStream object for an open disk device.
        /// </summary>
        /// <param name="SafeFileHandle">Open file handle for disk device.</param>
        /// <param name="AccessMode">Access to request for stream.</param>
        /// <param name="DiskSize">Size that should be returned by Length property</param>
        protected internal DiskStream(SafeFileHandle SafeFileHandle, FileAccess AccessMode, long DiskSize) : base(new FileStream(SafeFileHandle, AccessMode, bufferSize: 1), Alignment: NativeFileIO.GetDiskGeometry(SafeFileHandle)?.BytesPerSector ?? 512, ownsBaseStream: true)
        {
            _CachedLength = DiskSize;
        }

        public SafeFileHandle SafeFileHandle
        {
            get
            {
                return (FileStream)BaseStream.SafeFileHandle;
            }
        }

        /// <summary>
        /// Retrieves raw disk size.
        /// </summary>
        public override long Length
        {
            get
            {
                _CachedLength = _CachedLength ?? NativeFileIO.GetDiskSize(SafeFileHandle);

                return _CachedLength.Value;
            }
        }

        private bool _size_from_vbr;

        public bool SizeFromVBR
        {
            get
            {
                return _size_from_vbr;
            }
            set
            {
                if (Value)
                {
                    _CachedLength = GetVBRPartitionLength();
                    if (!_CachedLength.HasValue)
                        throw new NotSupportedException();
                }
                else
                {
                    _CachedLength = NativeFileIO.GetDiskSize(SafeFileHandle);
                    if (!_CachedLength.HasValue)
                        throw new NotSupportedException();
                }
                _size_from_vbr = Value;
            }
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get partition length as indicated by VBR. Valid for volumes with formatted file system.
        /// </summary>
        public long? GetVBRPartitionLength()
        {
            ;/* Cannot convert LocalDeclarationStatementSyntax, System.InvalidCastException: Unable to cast object of type 'Microsoft.CodeAnalysis.VisualBasic.Syntax.RangeArgumentSyntax' to type 'Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleArgumentSyntax'.
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertArrayBounds>b__20_0(ArgumentSyntax a)
   at System.Linq.Enumerable.WhereSelectEnumerableIterator`2.MoveNext()
   at Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SeparatedList[TNode](IEnumerable`1 nodes)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertArrayRankSpecifierSyntaxes(SyntaxList`1 arrayRankSpecifierSyntaxs, ArgumentListSyntax nodeArrayBounds, Boolean withSizes)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.AdjustFromName(TypeSyntax rawType, ModifiedIdentifierSyntax name, ExpressionSyntax initializer)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarations(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

        Dim vbr(0 To CInt(NativeFileIO.GetDiskGeometry(SafeFileHandle).Value.BytesPerSector - 1UI)) As Byte

 */
            Position = 0;

            if (Read(vbr, 0, vbr.Length) < vbr.Length)
                return default(Long?);

            var vbr_sector_size = BitConverter.ToInt16(vbr, 0xB);

            if (vbr_sector_size <= 0)
                return default(Long?);

            long total_sectors;

            total_sectors = BitConverter.ToUInt16(vbr, 0x13);

            if (total_sectors == 0)
                total_sectors = BitConverter.ToUInt32(vbr, 0x20);

            if (total_sectors == 0)
                total_sectors = BitConverter.ToInt64(vbr, 0x28);

            if (total_sectors < 0)
                return default(Long?);

            return total_sectors * vbr_sector_size;
        }
    }
}

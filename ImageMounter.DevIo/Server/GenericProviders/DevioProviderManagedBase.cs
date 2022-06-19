
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
using System.Runtime.InteropServices;

namespace ImageMounter.DevIo.Server.GenericProviders;

/// <summary>
///  Base class for implementing <see>IDevioProvider</see> interface with a storage backend where
///  bytes to read from and write to device are provided in a managed byte array.
///  </summary>
public abstract class DevioProviderManagedBase : IDevioProvider
{

    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler Disposed;

    /// <summary>
    /// Determines whether virtual disk is writable or read-only.
    /// </summary>
    /// <value>True if virtual disk can be written to through this instance, or False
    /// if it is opened for reading only.</value>
    /// <returns>True if virtual disk can be written to through this instance, or False
    /// if it is opened for reading only.</returns>
    public abstract bool CanWrite { get; }

    /// <summary>
    /// Indicates whether provider supports shared image operations with registrations
    /// and reservations.
    /// </summary>
    public virtual bool SupportsShared { get; }

    /// <summary>
    /// Size of virtual disk.
    /// </summary>
    /// <value>Size of virtual disk.</value>
    /// <returns>Size of virtual disk.</returns>
    public abstract long Length { get; }

    /// <summary>
    /// Sector size of virtual disk.
    /// </summary>
    /// <value>Sector size of virtual disk.</value>
    /// <returns>Sector size of virtual disk.</returns>
    public abstract uint SectorSize { get; }

    /// <summary>
    /// Reads bytes from virtual disk to a byte array.
    /// </summary>
    /// <param name="buffer">Byte array with enough size where read bytes are stored.</param>
    /// <param name="bufferoffset">Offset in array where bytes are stored.</param>
    /// <param name="count">Number of bytes to read from virtual disk device.</param>
    /// <param name="fileoffset">Offset at virtual disk device where read starts.</param>
    /// <returns>Returns number of bytes read from device that were stored in byte array.</returns>
    public abstract int Read(byte[] buffer, int bufferoffset, int count, long fileoffset);

    private readonly List<WeakReference> _buffers = new List<WeakReference>();

    private byte[] GetByteBuffer(int size)
    {

        /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
        /* TODO ERROR: Skipped EndIfDirectiveTrivia */
        var buffer = _buffers.Select(@ref => @ref.Target as byte[])
            .FirstOrDefault(buf => buf != null && buf.Length >= size);

        if (buffer == null)
        {
            ; /* Cannot convert AssignmentStatementSyntax, System.InvalidCastException: Unable to cast object of type 'Microsoft.CodeAnalysis.VisualBasic.Syntax.RangeArgumentSyntax' to type 'Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleArgumentSyntax'.
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.<ConvertArrayBounds>b__20_0(ArgumentSyntax a)
   at System.Linq.Enumerable.WhereSelectEnumerableIterator`2.MoveNext()
   at Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SeparatedList[TNode](IEnumerable`1 nodes)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertArrayRankSpecifierSyntaxes(SyntaxList`1 arrayRankSpecifierSyntaxs, ArgumentListSyntax nodeArrayBounds, Boolean withSizes)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ArrayCreationExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ArrayCreationExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitAssignmentStatement(AssignmentStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.AssignmentStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

#If TRACE_PERFORMANCE Then
                alloc_counter += 1
#End If

                buffer = New Byte(0 To size - 1) {}

 */
            var wr = _buffers.FirstOrDefault(@ref => !@ref.IsAlive);

            if (wr == null)
                _buffers.Add(new WeakReference(buffer));
            else
                wr.Target = buffer;
        }

        return buffer;
    }

    private int Read(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {
        var _byte_buffer = GetByteBuffer(count);

        var readlen = Read(_byte_buffer, 0, count, fileoffset);
        Marshal.Copy(_byte_buffer, 0, buffer + bufferoffset, readlen);

        return readlen;
    }

    /// <summary>
    /// Writes out bytes from byte array to virtual disk device.
    /// </summary>
    /// <param name="buffer">Byte array containing bytes to write out to device.</param>
    /// <param name="bufferoffset">Offset in array where bytes to write start.</param>
    /// <param name="count">Number of bytes to write to virtual disk device.</param>
    /// <param name="fileoffset">Offset at virtual disk device where write starts.</param>
    /// <returns>Returns number of bytes written to device.</returns>
    public abstract int Write(byte[] buffer, int bufferoffset, int count, long fileoffset);

    private int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {
        var _byte_buffer = GetByteBuffer(count);

        Marshal.Copy(buffer + bufferoffset, _byte_buffer, 0, count);

        return Write(_byte_buffer, 0, count, fileoffset);
    }

    /// <summary>
    /// Manage registrations and reservation keys for shared images.
    /// </summary>
    /// <param name="Request">Request data</param>
    /// <param name="Response">Response data</param>
    /// <param name="Keys">List of currently registered keys</param>
    public virtual void SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
    {
        throw new NotImplementedException();
    }

    public bool IsDisposed { get; } // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        OnDisposing(EventArgs.Empty);

        if (!IsDisposed)
        {
            if (disposing)
            {
            }
        }

        IsDisposed = true;

        OnDisposed(EventArgs.Empty);
    }

    // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
    ~DevioProviderManagedBase()
    {
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(false);
        base.Finalize();
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Raises Disposing event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposing(EventArgs e)
    {
        Disposing?.Invoke(this, e);
    }

    /// <summary>
    /// Raises Disposed event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposed(EventArgs e)
    {
        Disposed?.Invoke(this, e);
    }
}

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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;

namespace ImageMounter.DevIo.Server.Services
{

    /// <summary>
    /// Class that implements server end of Devio TCP/IP based communication protocol.
    /// It uses an object implementing <see>IDevioProvider</see> interface as storage backend
    /// for I/O requests received from client.
    /// </summary>
    public class DevioTcpService : DevioServiceBase
    {

        /// <summary>
        /// Server endpoint where this service listens for client connection.
        /// </summary>
        public IPEndPoint ListenEndPoint { get; }

        private Action InternalShutdownRequestAction;

        /// <summary>
        /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
        /// TCP/IP based communication.
        /// </summary>
        /// <param name="ListenAddress">IP address where service should listen for client connection.</param>
        /// <param name="ListenPort">IP port where service should listen for client connection.</param>
        /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        /// instance is disposed.</param>
        public DevioTcpService(IPAddress ListenAddress, int ListenPort, IDevioProvider DevioProvider, bool OwnsProvider) : base(DevioProvider, OwnsProvider)
        {
            ListenEndPoint = new IPEndPoint(ListenAddress, ListenPort);
        }

        /// <summary>
        /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
        /// TCP/IP based communication.
        /// </summary>
        /// <param name="ListenPort">IP port where service should listen for client connection. Instance will listen on all
        /// interfaces where this port is available.</param>
        /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        /// instance is disposed.</param>
        public DevioTcpService(int ListenPort, IDevioProvider DevioProvider, bool OwnsProvider) : base(DevioProvider, OwnsProvider)
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Any, ListenPort);
        }

        /// <summary>
        /// Runs service that acts as server end in Devio TCP/IP based communication. It will first wait for
        /// a client to connect, then serve client I/O requests and when client finally requests service to terminate, this
        /// method returns to caller. To run service in a worker thread that automatically disposes this object after client
        /// disconnection, call StartServiceThread() instead.
        /// </summary>
        public override void RunService()
        {
            try
            {
                Trace.WriteLine($"Setting up listener at {ListenEndPoint}");

                TcpListener Listener = new TcpListener(ListenEndPoint);

                try
                {
                    Listener.ExclusiveAddressUse = false;
                    Listener.Start();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Listen failed: {ex}");
                    Exception = new Exception("Listen failed on tcp port", ex);
                    OnServiceInitFailed(EventArgs.Empty);
                    return;
                }

                Trace.WriteLine("Raising service ready event.");
                OnServiceReady(EventArgs.Empty);

                EventHandler StopServiceThreadHandler = new EventHandler(() => Listener.Stop());
                StopServiceThread += StopServiceThreadHandler;
                var TcpSocket = Listener.AcceptSocket();
                StopServiceThread -= StopServiceThreadHandler;
                Listener.Stop();
                Trace.WriteLine($"Connection from {TcpSocket.RemoteEndPoint}");

                using (NetworkStream TcpStream = new NetworkStream(TcpSocket, ownsSocket: true))
                using (BinaryReader Reader = new BinaryReader(TcpStream, Encoding.Default))
                using (BinaryWriter Writer = new BinaryWriter(new MemoryStream(), Encoding.Default)
)
                {
                    InternalShutdownRequestAction = () =>
                    {
                        try
                        {
                            Reader.Dispose();
                        }
                        catch
                        {
                        }
                    };

                    byte[] ManagedBuffer = null;

                    do
                    {
                        IMDPROXY_REQ RequestCode;
                        try
                        {
                            RequestCode = (IMDPROXY_REQ)Reader.ReadUInt64();
                        }
                        catch (EndOfStreamException ex)
                        {
                            break;
                        }

                        // Trace.WriteLine("Got client request: " & RequestCode.ToString())

                        switch (RequestCode)
                        {
                            case object _ when IMDPROXY_REQ.IMDPROXY_REQ_INFO:
                                {
                                    SendInfo(Writer);
                                    break;
                                }

                            case object _ when IMDPROXY_REQ.IMDPROXY_REQ_READ:
                                {
                                    ReadData(Reader, Writer, ManagedBuffer);
                                    break;
                                }

                            case object _ when IMDPROXY_REQ.IMDPROXY_REQ_WRITE:
                                {
                                    WriteData(Reader, Writer, ManagedBuffer);
                                    break;
                                }

                            case object _ when IMDPROXY_REQ.IMDPROXY_REQ_CLOSE:
                                {
                                    Trace.WriteLine("Closing connection.");
                                    return;
                                }

                            default:
                                {
                                    Trace.WriteLine($"Unsupported request code: {RequestCode}");
                                    return;
                                }
                        }

                        // Trace.WriteLine("Sending response and waiting for next request.")

                        Writer.Seek(0, SeekOrigin.Begin);
                        {
                            var withBlock = (MemoryStream)Writer.BaseStream;
                            withBlock.WriteTo(TcpStream);
                            withBlock.SetLength(0);
                        }
                    }
                    while (true);
                }

                Trace.WriteLine("Client disconnected.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Unhandled exception in service thread: {ex}");
                OnServiceUnhandledException(new ThreadExceptionEventArgs(ex));
            }

            finally
            {
                OnServiceShutdown(EventArgs.Empty);
            }
        }

        private void SendInfo(BinaryWriter Writer)
        {
            Writer.Write(System.Convert.ToUInt64(DevioProvider.Length));
            Writer.Write(System.Convert.ToUInt64(REQUIRED_ALIGNMENT));
            Writer.Write(System.Convert.ToUInt64(DevioProvider.CanWrite ? IMDPROXY_FLAGS.IMDPROXY_FLAG_NONE : IMDPROXY_FLAGS.IMDPROXY_FLAG_RO));
        }

        private void ReadData(BinaryReader Reader, BinaryWriter Writer, byte[] Data)
        {
            var Offset = Reader.ReadInt64();
            var ReadLength = System.Convert.ToInt32(Reader.ReadUInt64());
            if (Data == null || Data.Length < ReadLength)
                Array.Resize(ref Data, ReadLength);
            ulong WriteLength;
            ulong ErrorCode;

            try
            {
                WriteLength = System.Convert.ToUInt64(DevioProvider.Read(Data, 0, ReadLength, Offset));
                ErrorCode = 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                Trace.WriteLine($"Read request at {Offset} for {ReadLength} bytes.");
                ErrorCode = 1;
                WriteLength = 0;
            }

            Writer.Write(ErrorCode);
            Writer.Write(WriteLength);
            if (WriteLength > 0)
                Writer.Write(Data, 0, System.Convert.ToInt32(WriteLength));
        }

        private void WriteData(BinaryReader Reader, BinaryWriter Writer, byte[] Data)
        {
            var Offset = Reader.ReadInt64();
            var Length = Reader.ReadUInt64();
            if (Data == null || Data.Length < Length)
                Array.Resize(ref Data, System.Convert.ToInt32(Length));

            var ReadLength = Reader.Read(Data, 0, System.Convert.ToInt32(Length));
            ulong WriteLength;
            ulong ErrorCode;

            try
            {
                WriteLength = System.Convert.ToUInt64(DevioProvider.Write(Data, 0, ReadLength, Offset));
                ErrorCode = 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                Trace.WriteLine($"Write request at {Offset} for {Length} bytes.");
                ErrorCode = 1;
                WriteLength = 0;
            }

            Writer.Write(ErrorCode);
            Writer.Write(WriteLength);
        }

        protected override string ProxyObjectName
        {
            get
            {
                var EndPoint = ListenEndPoint;
                if (EndPoint.Address.Equals(IPAddress.Any))
                    EndPoint = new IPEndPoint(IPAddress.Loopback, EndPoint.Port);
                return EndPoint.ToString();
            }
        }

        protected override DeviceFlags ProxyModeFlags
        {
            get
            {
                return DeviceFlags.TypeProxy | DeviceFlags.ProxyTypeTCP;
            }
        }

        protected override void EmergencyStopServiceThread()
        {
            ;/* Cannot convert ExpressionStatementSyntax, System.ArgumentNullException: Value cannot be null.
Parameter name: node
   at Microsoft.CodeAnalysis.VisualBasic.VBSemanticModel.GetSymbolInfoForNode(SyntaxNode node, CancellationToken cancellationToken)
   at Microsoft.CodeAnalysis.VisualBasic.VBSemanticModel.GetSymbolInfoCore(SyntaxNode node, CancellationToken cancellationToken)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitInvocationExpression(InvocationExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitInvocationExpression(InvocationExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ConditionalAccessExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ConditionalAccessExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitExpressionStatement(ExpressionStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.ExpressionStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 

            InternalShutdownRequestAction?()

 */
        }
    }
}

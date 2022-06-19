﻿// '''' DevioServiceBase.vb
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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

using ImageMounter.Devio.Interop.Server.GenericProviders;
using ImageMounter.Interop.IO;
using ImageMounter.IO;

namespace ImageMounter.DevIo.Server.Services
{

    /// <summary>
    /// Base class for classes that implement functionality for acting as server end of
    /// Devio communication. Derived classes implement communication mechanisms and
    /// use an object implementing <see>IDevioProvider</see> interface as storage backend
    /// for I/O requests received from client.
    /// </summary>
    public abstract class DevioServiceBase : IVirtualDiskService
    {
        public Exception Exception { get; set; }

        protected Thread ServiceThread { get; }

        /// <summary>
        /// IDevioProvider object used by this instance.
        /// </summary>
        public IDevioProvider DevioProvider { get; }

        /// <summary>
        /// ScsiAdapter object used when StartServiceThreadAndMount was called. This object
        /// is used to remove the device when DismountAndStopServiceThread is called.
        /// </summary>
        /// <returns></returns>
        public ScsiAdapter ScsiAdapter { get; }

        /// <summary>
        /// Indicates whether DevioProvider will be automatically closed when this instance
        /// is disposed.
        /// </summary>
        public bool OwnsProvider { get; }

        /// <summary>
        /// Size of virtual disk device.
        /// </summary>
        /// <value>Size of virtual disk device.</value>
        /// <returns>Size of virtual disk device.</returns>
        public virtual long DiskSize { get; set; }

        /// <summary>
        /// Offset in disk image where this virtual disk device begins.
        /// </summary>
        /// <value>Offset in disk image where this virtual disk device begins.</value>
        /// <returns>Offset in disk image where this virtual disk device begins.</returns>
        public virtual long Offset { get; set; }

        /// <summary>
        /// Sector size of virtual disk device.
        /// </summary>
        /// <value>Sector size of virtual disk device.</value>
        /// <returns>Sector size of virtual disk device.</returns>
        public virtual uint SectorSize { get; set; }

        /// <summary>
        /// Description of service.
        /// </summary>
        /// <value>Description of service.</value>
        /// <returns>Description of service.</returns>
        public virtual string Description { get; set; }

        /// <summary>
        /// Event raised when service thread is ready to start accepting connection from a client.
        /// </summary>
        public event EventHandler ServiceReady;

        protected virtual void OnServiceReady(EventArgs e)
        {
            ServiceReady?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when service initialization fails.
        /// </summary>
        public event EventHandler ServiceInitFailed;

        protected virtual void OnServiceInitFailed(EventArgs e)
        {
            ServiceInitFailed?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when an Arsenal Image Mounter Disk Device is created by with this instance.
        /// </summary>
        public event EventHandler DiskDeviceCreated;

        protected virtual void OnDiskDeviceCreated(EventArgs e)
        {
            DiskDeviceCreated?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when any of the DismountAndStopServiceThread methods are called, before
        /// disk device object is removed. Note that this event is not raised if device is directly
        /// removed by some other method.
        /// </summary>
        public event EventHandler ServiceStopping;

        protected virtual void OnServiceStopping(EventArgs e)
        {
            ServiceStopping?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when service thread exits.
        /// </summary>
        public event EventHandler ServiceShutdown;

        protected virtual void OnServiceShutdown(EventArgs e)
        {
            ServiceShutdown?.Invoke(this, e);
        }

        /// <summary>
        /// Event raised when an unhandled exception occurs in service thread and thread is about to terminate,
        /// but before associated virtual disk device is forcefully removed, as specified by ForceRemoveDiskDeviceOnCrash
        /// property.
        /// </summary>
        public event ThreadExceptionEventHandler ServiceUnhandledException;

        protected virtual void OnServiceUnhandledException(ThreadExceptionEventArgs e)
        {
            ServiceUnhandledException?.Invoke(this, e);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && HasDiskDevice && ForceRemoveDiskDeviceOnCrash &&
                ScsiAdapter != null)
                ScsiAdapter.RemoveDevice(_DiskDeviceNumber);
        }

        /// <summary>
        /// Event raised to stop service thread. Service thread handle this event by preparing communication for
        /// disconnection.
        /// </summary>
        protected event EventHandler StopServiceThread;

        protected virtual void OnStopServiceThread(EventArgs e)
        {
            StopServiceThread?.Invoke(this, e);
        }

        /// <summary>
        /// Creates a new service instance with enough data to later run a service that acts as server end in Devio
        /// communication.
        /// </summary>
        /// <param name="DevioProvider">IDevioProvider object to that serves as storage backend for this service.</param>
        /// <param name="OwnsProvider">Indicates whether DevioProvider object will be automatically closed when this
        /// instance is disposed.</param>
        protected DevioServiceBase(IDevioProvider DevioProvider, bool OwnsProvider)
        {
            this.OwnsProvider = OwnsProvider;

            DevioProvider = DevioProvider.NullCheck(nameof(DevioProvider));

            DiskSize = DevioProvider.Length;

            SectorSize = DevioProvider.SectorSize;
        }

        /// <summary>
        /// When overridden in a derived class, runs service that acts as server end in Devio communication. It will
        /// first wait for a client to connect, then serve client I/O requests and when client finally requests service to
        /// terminate, this method returns to caller. To run service in a worker thread that automatically disposes this
        /// object after client disconnection, call StartServiceThread() instead.
        /// </summary>
        public abstract void RunService();

        /// <summary>
        /// When overridden in a derived class, immediately stop service thread. This method will be called internally when
        /// service base class methods for example detect that the device object no longer exists in the driver, or similar
        /// scenarios where the driver cannot be requested to request service thread to shut down.
        /// </summary>
        protected abstract void EmergencyStopServiceThread();

        /// <summary>
        /// Creates a worker thread where RunService() method is called. After that method exits, this instance is automatically
        /// disposed.
        /// </summary>
        public virtual bool StartServiceThread()
        {
            using (ManualResetEvent ServiceReadyEvent = new ManualResetEvent(initialState: false))
            using (ManualResetEvent ServiceInitFailedEvent = new ManualResetEvent(initialState: false)
                  )
            {
                EventHandler ServiceReadyHandler = new EventHandler(() => ServiceReadyEvent.Set());
                ServiceReady += ServiceReadyHandler;
                EventHandler ServiceInitFailedHandler = new EventHandler(() => ServiceInitFailedEvent.Set());
                ServiceInitFailed += ServiceInitFailedHandler;

                ServiceThread = new Thread(ServiceThreadProcedure);
                ServiceThread.Start();
                WaitHandle.WaitAny(new[] { ServiceReadyEvent, ServiceInitFailedEvent });

                ServiceReady -= ServiceReadyHandler;
                ServiceInitFailed -= ServiceInitFailedHandler;

                if (ServiceReadyEvent.WaitOne(0))
                    return true;
                else
                    return false;
            }
        }

        private void ServiceThreadProcedure()
        {
            try
            {
                RunService();
            }
            finally
            {
                Dispose();
            }
        }

        /// <summary>
        /// Waits for service thread created by StartServiceThread() to exit. If no service thread
        /// has been created or if it has already exit, this method returns immediately with a
        /// value of True.
        /// </summary>
        /// <param name="timeout">Timeout value, or Timeout.Infinite to wait infinitely.</param>
        /// <returns>Returns True if service thread has exit or no service thread has been
        /// created, or False if timeout occurred.</returns>
        public virtual bool WaitForServiceThreadExit(TimeSpan timeout)
        {
            if (ServiceThread != null && ServiceThread.ManagedThreadId != Thread.CurrentThread.ManagedThreadId &&
                ServiceThread.IsAlive)
            {
                Trace.WriteLine($"Waiting for service thread to terminate.");

                return ServiceThread.Join(timeout);
            }
            else
                return true;
        }

        /// <summary>
        /// Waits for service thread created by StartServiceThread() to exit. If no service thread
        /// has been created or if it has already exit, this method returns immediately.
        /// </summary>
        public virtual void WaitForServiceThreadExit()
        {
            if (ServiceThread != null && ServiceThread.ManagedThreadId != Thread.CurrentThread.ManagedThreadId &&
                ServiceThread.IsAlive)
            {
                Trace.WriteLine($"Waiting for service thread to terminate.");

                ServiceThread.Join();
            }
        }

        /// <summary>
        /// Combines a call to StartServiceThread() with a call to API to create a proxy type
        /// Arsenal Image Mounter Disk Device that uses the started service as storage backend.
        /// </summary>
        /// <param name="Flags">Flags to pass to API.CreateDevice() combined with fixed flag
        /// values specific to this instance. Example of such fixed flag values are flags specifying
        /// proxy operation and which proxy communication protocol to use, which therefore do not
        /// need to be specified in this parameter. A common value to pass however, is DeviceFlags.ReadOnly
        /// to create a read-only virtual disk device.</param>
        public virtual void StartServiceThreadAndMount(ScsiAdapter ScsiAdapter, DeviceFlags Flags)
        {
            ScsiAdapter = ScsiAdapter.NullCheck(nameof(ScsiAdapter));

            if (!StartServiceThread())
            {
                if (Exception == null)
                    throw new Exception("Service initialization failed");
                else
                    throw new Exception("Service initialization failed", Exception);
            }

            try
            {
                ScsiAdapter.CreateDevice(DiskSize, SectorSize, Offset, Flags | AdditionalFlags | ProxyModeFlags,
                    ProxyObjectName, false, WriteOverlayImageName, false, _DiskDeviceNumber);

                OnDiskDeviceCreated(EventArgs.Empty);
            }
            catch (Exception ex)
            {
                OnStopServiceThread(EventArgs.Empty);

                throw new Exception($"Error when starting service thread or mounting {ProxyObjectName}", ex);
            }
        }

        /// <summary>
        /// Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount() and waits
        /// for service thread of this instance to exit.
        /// </summary>
        public virtual void DismountAndStopServiceThread()
        {
            RemoveDeviceAndStopServiceThread();

            WaitForServiceThreadExit();
        }

        /// <summary>
        /// Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount() and waits
        /// for service thread of this instance to exit.
        /// </summary>
        /// <param name="timeout">Timeout value to wait for service thread exit, or Timeout.Infinite to wait infinitely.</param>
        public virtual bool DismountAndStopServiceThread(TimeSpan timeout)
        {
            RemoveDeviceAndStopServiceThread();

            var rc = WaitForServiceThreadExit(timeout);

            if (rc)
                Trace.WriteLine($"Service for device {_DiskDeviceNumber} shut down successfully.");
            else
                Trace.WriteLine($"Service for device {_DiskDeviceNumber} shut down timed out.");

            return rc;
        }

        /// <summary>
        /// Dismounts an Arsenal Image Mounter Disk Device created by StartServiceThreadAndMount(). If device
        /// was already removed, it calls EmergencyStopServiceThread() to notify service thread.
        /// </summary>
        protected void RemoveDeviceAndStopServiceThread()
        {
            Trace.WriteLine($"Notifying service stopping for device {_DiskDeviceNumber}...");

            OnServiceStopping(EventArgs.Empty);

            Trace.WriteLine($"Removing device {_DiskDeviceNumber}...");

            var i = 1;
            do
            {
                try
                {
                    ScsiAdapter.RemoveDevice(_DiskDeviceNumber);
                    Trace.WriteLine($"Device {_DiskDeviceNumber} removed.");
                    break;
                }
                catch (Win32Exception ex) when ((i < 40 && ex.NativeErrorCode ==
                                                    NativeConstants.ERROR_ACCESS_DENIED))
                {
                    Trace.WriteLine($"Access denied attempting to remove device {_DiskDeviceNumber}, retrying...");
                    i += 1;
                    Thread.Sleep(100);
                    continue;
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == NativeConstants.ERROR_FILE_NOT_FOUND)
                {
                    Trace.WriteLine($"Attempt to remove non-existent device {_DiskDeviceNumber}");
                    EmergencyStopServiceThread();
                    break;
                }
            } while (true);
        }

        /// <summary>
        /// Additional flags that will be passed to API.CreateDevice() in StartServiceThreadAndMount()
        /// method. Default value of this property depends on derived class and which parameters are normally
        /// needed for driver to start communication with this service.
        /// </summary>
        /// <value>Default value of this property depends on derived class and which parameters are normally
        /// needed for driver to start communication with this service.</value>
        /// <returns>Default value of this property depends on derived class and which parameters are normally
        /// needed for driver to start communication with this service.</returns>
        public virtual DeviceFlags AdditionalFlags { get; set; }

        /// <summary>
        /// When overridden in a derived class, indicates additional flags that will be passed to
        /// API.CreateDevice() in StartServiceThreadAndMount() method. Value of this property depends
        /// on derived class and which parameters are normally needed for driver to start communication with this
        /// service.
        /// </summary>
        /// <value>Default value of this property depends on derived class and which parameters are normally
        /// needed for driver to start communication with this service.</value>
        /// <returns>Default value of this property depends on derived class and which parameters are normally
        /// needed for driver to start communication with this service.</returns>
        protected abstract DeviceFlags ProxyModeFlags { get; }

        /// <summary>
        /// Object name that Arsenal Image Mounter can use to connect to this service.
        /// </summary>
        /// <value>Object name string.</value>
        /// <returns>Object name that Arsenal Image Mounter can use to connect to this service.</returns>
        protected abstract string ProxyObjectName { get; }

        /// <summary>
        /// Path to write overlay image to pass to driver when a virtual disk is created for this service.
        /// </summary>
        /// <value>Path to write overlay image to pass to driver.</value>
        /// <returns>Path to write overlay image to pass to driver.</returns>
        public string WriteOverlayImageName { get; set; }

        private uint _DiskDeviceNumber = uint.MaxValue;

        /// <summary>
        /// After successful call to StartServiceThreadAndMount(), this property returns disk device
        /// number for created Arsenal Image Mounter Disk Device. This number can be used when calling API
        /// functions. If no Arsenal Image Mounter Disk Device has been created by this instance, an exception is
        /// thrown. Use HasDiskDevice property to find out if a disk device has been created.
        /// </summary>
        /// <value>Disk device
        /// number for created Arsenal Image Mounter Disk Device.</value>
        /// <returns>Disk device
        /// number for created Arsenal Image Mounter Disk Device.</returns>
        /// <remarks></remarks>
        public uint DiskDeviceNumber
        {
            get
            {
                if (_DiskDeviceNumber == uint.MaxValue)
                    throw new InvalidOperationException(
                        "No Arsenal Image Mounter Disk Device currently associated with this instance.");
                return _DiskDeviceNumber;
            }
        }

        /// <summary>
        /// Use HasDiskDevice property to find out if a disk device has been created in a call to
        /// StartServiceThreadAndMount() method. Use DiskDeviceNumber property to find out disk
        /// device number for created device.
        /// </summary>
        /// <value>Returns True if an Arsenal Image Mounter Disk Device has been created, False otherwise.</value>
        /// <returns>Returns True if an Arsenal Image Mounter Disk Device has been created, False otherwise.</returns>
        public virtual bool HasDiskDevice
        {
            get { return _DiskDeviceNumber != uint.MaxValue; }
        }

        /// <summary>
        /// Opens a DiskDevice object for direct access to a mounted device provided by
        /// this service instance.
        /// </summary>
        public virtual DiskDevice OpenDiskDevice(FileAccess access)
        {
            return ScsiAdapter.OpenDevice(DiskDeviceNumber, access);
        }

        /// <summary>
        /// Returns a PhysicalDrive or CdRom device name for a mounted device provided by
        /// this service instance.
        /// </summary>
        public virtual string GetDiskDeviceName()
        {
            return ScsiAdapter.GetDeviceName(DiskDeviceNumber);
        }

        /// <summary>
        /// Deletes the write overlay image file after use. Also sets this filter driver to
        /// silently ignore flush requests to improve performance when integrity of the write
        /// overlay image is not needed for future sessions.
        /// </summary>
        /// <returns>Returns 0 on success or Win32 error code on failure</returns>
        public int SetWriteOverlayDeleteOnClose()
        {
            using (var disk = OpenDiskDevice(FileAccess.ReadWrite))
            {
                return API.SetWriteOverlayDeleteOnClose(disk.SafeFileHandle);
            }
        }

        /// <summary>
        /// Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        /// forcefully removed if a crash occurs in service thread of this instance. Default is True.
        /// </summary>
        /// <value>Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        /// forcefully removed if a crash occurs in service thread of this instance. Default is True.</value>
        /// <returns>Indicates whether Arsenal Image Mounter Disk Device created by this instance will be automatically
        /// forcefully removed if a crash occurs in service thread of this instance. Default is True.</returns>
        public virtual bool ForceRemoveDiskDeviceOnCrash { get; set; } = true;

        public virtual void RemoveDevice()
        {
            ScsiAdapter.RemoveDevice(DiskDeviceNumber);
        }

        public virtual void RemoveDeviceSafe()
        {
            ScsiAdapter.RemoveDeviceSafe(DiskDeviceNumber);
        }

        public bool IsDisposed { get; } // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!this.IsDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (HasDiskDevice)
                    {
                        try
                        {
                            /* TODO ERROR: Skipped IfDirectiveTrivia */ /* TODO ERROR: Skipped DisabledTextTrivia */
                            /* TODO ERROR: Skipped EndIfDirectiveTrivia */
                            DismountAndStopServiceThread();
                        }
                        catch
                        {
                        }
                    }
                    else
                        try
                        {
                            OnStopServiceThread(EventArgs.Empty);
                        }
                        catch
                        {
                        }

                    if (OwnsProvider)
                        DevioProvider?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                // TODO: set large fields to null.
                DevioProvider = null;
            }

            this.IsDisposed = true;
        }

        // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        ~DevioServiceBase()
        {
            // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(false);
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
    }
}
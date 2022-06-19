using System.Runtime.Versioning;
using System.Security.AccessControl;
using ImageMounter.IO.Native;

namespace ImageMounter.IO
{
    /// <summary>
    /// Represents a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
    /// </summary>
    public class SystemNotificationEvent : WaitHandle
    {

        /// <summary>
        ///     Opens a system notification event object. Well known paths are available as constants of SystemNotificationEvent class.
        ///     </summary>
        ///     <param name="EventName">NT name and path to event to open</param>
        public SystemNotificationEvent(string EventName)
        {
            WaitHandle.SafeWaitHandle = NativeFileIO.NtOpenEvent(EventName, 0, FileSystemRights.Synchronize | NativeConstants.EVENT_QUERY_STATE, null/* TODO Change to default(_) if this is not a reference type */);
        }

        public const string PrefetchTracesReady = @"\KernelObjects\PrefetchTracesReady";
        public const string MemoryErrors = @"\KernelObjects\MemoryErrors";
        public const string LowNonPagedPoolCondition = @"\KernelObjects\LowNonPagedPoolCondition";
        public const string SuperfetchScenarioNotify = @"\KernelObjects\SuperfetchScenarioNotify";
        public const string SuperfetchParametersChanged = @"\KernelObjects\SuperfetchParametersChanged";
        public const string SuperfetchTracesReady = @"\KernelObjects\SuperfetchTracesReady";
        public const string PhysicalMemoryChange = @"\KernelObjects\PhysicalMemoryChange";
        public const string HighCommitCondition = @"\KernelObjects\HighCommitCondition";
        public const string HighMemoryCondition = @"\KernelObjects\HighMemoryCondition";
        public const string HighNonPagedPoolCondition = @"\KernelObjects\HighNonPagedPoolCondition";
        public const string SystemErrorPortReady = @"\KernelObjects\SystemErrorPortReady";
        public const string MaximumCommitCondition = @"\KernelObjects\MaximumCommitCondition";
        public const string LowCommitCondition = @"\KernelObjects\LowCommitCondition";
        public const string HighPagedPoolCondition = @"\KernelObjects\HighPagedPoolCondition";
        public const string LowMemoryCondition = @"\KernelObjects\LowMemoryCondition";
        public const string LowPagedPoolCondition = @"\KernelObjects\LowPagedPoolCondition";
    }

    public sealed class RegisteredEventHandler : IDisposable
    {
        private readonly RegisteredWaitHandle _registered_wait_handle;

        public WaitHandle WaitHandle { get; }
        public EventHandler EventHandler { get; }

        public RegisteredEventHandler(WaitHandle waitObject, EventHandler handler)
        {
            WaitHandle = waitObject;

            EventHandler = handler;

            _registered_wait_handle = ThreadPool.RegisterWaitForSingleObject(waitObject, Callback, this, -1, executeOnlyOnce: true);
        }

        private static void Callback(object state, bool timedOut)
        {
            var obj = state as RegisteredEventHandler;

            obj?.EventHandler?.Invoke(obj.WaitHandle, EventArgs.Empty);
        }

        public void Dispose()
        {
            _registered_wait_handle?.Unregister(null);

            GC.SuppressFinalize(this);
        }
    }

    public class WaitEventHandler
    {
        public WaitHandle WaitHandle { get; }

        private readonly List<RegisteredEventHandler> _event_handlers = new List<RegisteredEventHandler>();

        public WaitEventHandler(WaitHandle WaitHandle)
        {
            WaitHandle = WaitHandle;
        }

        public event EventHandler Signalled
        {
            add
            {
                _event_handlers.Add(new RegisteredEventHandler(WaitHandle, value));
            }
            remove
            {
                _event_handlers.RemoveAll(handler =>
                {
                    if (handler.EventHandler.Equals(value))
                    {
                        handler.Dispose();
                        return true;
                    }
                    else
                        return false;
                });
            }
        }
        void OnSignalled(object sender, EventArgs e)
        {
            _event_handlers.ForEach(handler => handler.EventHandler?.Invoke(sender, e));
        }
    }
}

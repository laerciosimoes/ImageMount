using System.Runtime.CompilerServices;

namespace ImageMounter.Interop.IO
{
    public sealed class WaitHandleAwaiter : INotifyCompletion
    {
        private readonly WaitHandle handle;
        private readonly TimeSpan timeout;
        private bool result;

        public WaitHandleAwaiter(WaitHandle handle, TimeSpan timeout)
        {
            this.handle = handle;
            this.timeout = timeout;
        }

        public WaitHandleAwaiter GetAwaiter() => this;

        public bool IsCompleted => handle.WaitOne(0);

        public bool GetResult() => result;

        private sealed class CompletionValues
        {
            public RegisteredWaitHandle callbackHandle;

            public Action continuation;

            public WaitHandleAwaiter awaiter;
        }

        public void OnCompleted(Action continuation)
        {
            var completionValues = new CompletionValues
            {
                continuation = continuation,
                awaiter = this
            };

            completionValues.callbackHandle = ThreadPool.RegisterWaitForSingleObject(
                waitObject: handle,
                callBack: WaitProc,
                state: completionValues,
                timeout: timeout,
                executeOnlyOnce: true);
        }

        private static void WaitProc(object state, bool timedOut)
        {
            var obj = state as CompletionValues;

            obj.awaiter.result = !timedOut;

            while (obj.callbackHandle is null)
            {
                Thread.Sleep(0);
            }

            obj.callbackHandle.Unregister(null);

            obj.continuation();
        }
    }
}

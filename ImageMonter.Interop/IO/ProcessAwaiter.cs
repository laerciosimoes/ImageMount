using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ImageMounter.Interop.IO
{

    public struct ProcessAwaiter : INotifyCompletion
    {
        public Process Process { get; }

        public ProcessAwaiter(Process process)
        {
            try
            {
                if (process is null || process.Handle == IntPtr.Zero)
                {
                    Process = null;
                    return;
                }

                if (!process.EnableRaisingEvents)
                {
                    throw new NotSupportedException("Events not available for this Process object.");
                }
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("ProcessAwaiter requires a local, running Process object with EnableRaisingEvents property set to true when Process object was created.", ex);
            }

            Process = process;
        }

        public ProcessAwaiter GetAwaiter() => this;

        public bool IsCompleted => Process.HasExited;

        public int GetResult() => Process.ExitCode;

        public void OnCompleted(Action continuation)
        {
            var completionCounter = 0;

            Process.Exited += (sender, e) =>
            {
                if (Interlocked.Exchange(ref completionCounter, 1) == 0)
                {
                    continuation();
                }
            };

            if (Process.HasExited && Interlocked.Exchange(ref completionCounter, 1) == 0)
            {
                continuation();
            }
        }
    }


}

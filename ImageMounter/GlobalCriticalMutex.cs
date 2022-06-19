using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageMounter
{

    public sealed class GlobalCriticalMutex : IDisposable
    {
        private const var GlobalCriticalSectionMutexName = @"Global\AIMCriticalOperation";

        private Mutex mutex;

        public bool WasAbandoned { get; }

        public GlobalCriticalMutex()
        {
            bool createdNew = default(Boolean);

            mutex = new Mutex(initiallyOwned: true, name: GlobalCriticalSectionMutexName, createdNew: ref createdNew);

            try
            {
                if (!createdNew)
                    mutex.WaitOne();
            }
            catch (AbandonedMutexException ex)
            {
                WasAbandoned = true;
            }

            catch (Exception ex)
            {
                mutex.Dispose();

                throw new Exception("Error entering global critical section for Arsenal Image Mounter driver", ex);
            }
        }

        private bool disposedValue; // To detect redundant calls

        // IDisposable
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                // TODO: set large fields to null.
                mutex = null;
            }

            disposedValue = true;
        }

        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
        }
    }
}
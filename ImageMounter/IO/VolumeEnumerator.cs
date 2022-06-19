using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageMounter.IO
{
    public class VolumeEnumerator : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator()
        {
            return new Enumerator();
        }

        private IEnumerator IEnumerable_GetEnumerator()
        {
            return GetEnumerator();
        }

        private class Enumerator : IEnumerator<string>
        {
            public SafeFindVolumeHandle SafeHandle { get; }

            private StringBuilder _sb = new StringBuilder(50);

            public string Current
            {
                get
                {
                    if (disposedValue)
                        throw new ObjectDisposedException("VolumeEnumerator.Enumerator");

                    return _sb.ToString();
                }
            }

            private object IEnumerator_Current
            {
                get
                {
                    return Current;
                }
            }

            public bool MoveNext()
            {
                if (disposedValue)
                    throw new ObjectDisposedException("VolumeEnumerator.Enumerator");

                if (SafeHandle == null)
                {
                    SafeHandle = FindFirstVolume(_sb, _sb.Capacity);
                    if (!SafeHandle.IsInvalid)
                        return true;
                    else if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_FILES)
                        return false;
                    else
                        throw new Win32Exception();
                }
                else if (FindNextVolume(SafeHandle, _sb, _sb.Capacity))
                    return true;
                else if (Marshal.GetLastWin32Error() == ERROR_NO_MORE_FILES)
                    return false;
                else
                    throw new Win32Exception();
            }

            private void Reset()
            {
                throw new NotImplementedException();
            }

            private bool disposedValue; // To detect redundant calls

            // IDisposable
            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposedValue)
                {
                    if (disposing)
                        // TODO: dispose managed state (managed objects).
                        SafeHandle?.Dispose();

                    // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                    SafeHandle = null;

                    // TODO: set large fields to null.
                    _sb.Clear();
                    _sb = null;
                }
                this.disposedValue = true;
            }

            // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
            ~Enumerator()
            {
                // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
                Dispose(false);
                base.Finalize();
            }

            // This code added by Visual Basic to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
    }
}

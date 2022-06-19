using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace ImageMounter.IO
{
    public class ConsoleProgressBar : IDisposable
    {
        public Timer Timer { get; } = new Timer(Tick);

        public int CurrentValue { get; }

        private readonly Func<double> updateFunc;

        private ConsoleProgressBar(Func<double> update)
        {
            updateFunc = update;
            CreateConsoleProgressBar();
        }

        public ConsoleProgressBar(int dueTime, int period, Func<double> update) : this(update)
        {
            Timer.Change(dueTime, period);
        }

        public ConsoleProgressBar(TimeSpan dueTime, TimeSpan period, Func<double> update) : this(update)
        {
            Timer.Change(dueTime, period);
        }

        private void Tick(object o)
        {
            var newvalue = updateFunc();

            if (newvalue != 1M && System.Convert.ToInt32(100 * newvalue) == CurrentValue)
                return;

            UpdateConsoleProgressBar(newvalue);
        }

        public static void CreateConsoleProgressBar()
        {
            if (IsConsoleOutputRedirected())
                return;

            StringBuilder row = new StringBuilder(Console.WindowWidth);

            row.Append('[');

            row.Append('.', Math.Max(Console.WindowWidth - 3, 0));

            row.Append(']');

            row.Append(Constants.vbCr[0]);

            lock (ConsoleSync)
            {
                Console.ForegroundColor = ConsoleProgressBarColor;

                Console.Write(row.ToString());

                Console.ResetColor();
            }
        }

        public static void UpdateConsoleProgressBar(double value)
        {
            if (IsConsoleOutputRedirected())
                return;

            if (value > 1M)
                value = 1M;
            else if (value < 0)
                value = 0M;

            var currentPos = System.Convert.ToInt32((Console.WindowWidth - 3) * value);

            StringBuilder row = new StringBuilder(Console.WindowWidth);

            row.Append('[');

            row.Append('=', Math.Max(currentPos, 0));

            row.Append('.', Math.Max(Console.WindowWidth - 3 - currentPos, 0));

            var percent = $" {100 * value} % ";

            var midpos = (Console.WindowWidth - 3 - percent.Length) >> 1;

            if (midpos > 0 && row.Length >= percent.Length)
            {
                row.Remove(midpos, percent.Length);

                row.Insert(midpos, percent);
            }

            row.Append(']');

            row.Append(Constants.vbCr[0]);

            lock (ConsoleSync)
            {
                Console.ForegroundColor = ConsoleProgressBarColor;

                Console.Write(row.ToString());

                Console.ResetColor();
            }
        }

        public static void FinishConsoleProgressBar()
        {
            UpdateConsoleProgressBar(1M);

            Console.WriteLine();
        }

        public static ConsoleColor ConsoleProgressBarColor { get; set; } = ConsoleColor.Cyan;

        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Timer.Dispose();
                    FinishConsoleProgressBar();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

                // TODO: set large fields to null.
                Timer = null;
            }

            disposedValue = true;
        }

        // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        ~ConsoleProgressBar()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(false);
            base.Finalize();
        }

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            // TODO: uncomment the following line if Finalize() is overridden above.
            GC.SuppressFinalize(this);
        }
    }
}
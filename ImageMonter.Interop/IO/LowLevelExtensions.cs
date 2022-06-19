﻿using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using IByteCollection = System.Collections.Generic.IReadOnlyCollection<byte>;


namespace ImageMounter.Interop.IO;

public static partial class LowLevelExtensions
{
    public static IEnumerable<Exception> Enumerate(this Exception ex)
    {
        while (ex is not null)
        {
            if (ex is TargetInvocationException)
            {
                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
            else if (ex is AggregateException aex)
            {
                foreach (var iex in aex.InnerExceptions.SelectMany(Enumerate))
                {
                    yield return iex;
                }

                yield break;
            }
            else if (ex is ReflectionTypeLoadException rtlex)
            {
                yield return ex;

                foreach (var iex in rtlex.LoaderExceptions.SelectMany(Enumerate))
                {
                    yield return iex;
                }

                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
            else
            {
                yield return ex;

                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
        }
    }

    public static IEnumerable<string> EnumerateMessages(this Exception ex)
    {
        while (ex is not null)
        {
            if (ex is TargetInvocationException)
            {
                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
            else if (ex is AggregateException agex)
            {
                foreach (var msg in agex.InnerExceptions.SelectMany(EnumerateMessages))
                {
                    yield return msg;
                }

                yield break;
            }
            else if (ex is ReflectionTypeLoadException tlex)
            {
                yield return ex.Message;

                foreach (var msg in tlex.LoaderExceptions.SelectMany(EnumerateMessages))
                {
                    yield return msg;
                }

                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
            else if (ex is Win32Exception win32ex)
            {
                yield return $"{win32ex.Message} ({win32ex.NativeErrorCode})";

                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
            else
            {
                yield return ex.Message;

                Debug.Assert(ex.InnerException != null, "ex.InnerException != null");
                ex = ex.InnerException;
            }
        }
    }

    public static string JoinMessages(this Exception exception) =>
        exception.JoinMessages(Environment.NewLine + Environment.NewLine);

    public static string JoinMessages(this Exception exception, string separator) =>
        string.Join(separator, exception.EnumerateMessages());

    public static string FormatLogMessages(this Exception exception) =>
            exception.JoinMessages();

    public static string CalculateChecksum<THashAlgorithm>(string file) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var stream = File.OpenRead(file))
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(stream);
        }

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(Stream stream) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(stream);
        }

        return hash.ToHexString();
    }

    public static string CalculateChecksum<THashAlgorithm>(this byte[] data) where THashAlgorithm : HashAlgorithm, new()
    {
        byte[] hash;
        using (var hashprovider = new THashAlgorithm())
        {
            hash = hashprovider.ComputeHash(data);
        }

        return hash.ToHexString();
    }

    public static string ToHexString(this IByteCollection data) => data.ToHexString(null);

    public static string ToHexString(this IByteCollection data, string delimiter)
    {
        
        if (data.Count == 0)
        {
            return string.Empty;
        }

        var capacity = data.Count << 1;
        capacity += delimiter.Length * (data.Count - 1);

        var result = new StringBuilder(capacity);

        foreach (var b in data)
        {
            if (delimiter is not null && result.Length > 0)
            {
                result.Append(delimiter);
            }
            result.Append(b.ToString("x2", NumberFormatInfo.InvariantInfo));
        }

        return result.ToString();
    }

    public static string ToHexString(this ReadOnlySpan<byte> data) => data.ToHexString(null);

    public static string ToHexString(this ReadOnlySpan<byte> data, string delimiter)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var capacity = data.Length << 1;
        capacity += delimiter.Length * (data.Length - 1);

        var result = new StringBuilder(capacity);

        foreach (var b in data)
        {
            if (delimiter is not null && result.Length > 0)
            {
                result.Append(delimiter);
            }
            result.Append(b.ToString("x2", NumberFormatInfo.InvariantInfo));
        }

        return result.ToString();
    }
    
    public static TextWriter WriteHex(this TextWriter writer, IEnumerable<byte> bytes)
    {
        var i = 0;
        foreach (var line in bytes.FormatHexLines())
        {
            writer.Write(((ushort)(i >> 16)).ToString("X4"));
            writer.Write(' ');
            writer.Write(((ushort)i).ToString("X4"));
            writer.Write("  ");
            writer.WriteLine(line);
            i += 0x10;
        }

        return writer;
    }

    public static IEnumerable<string> FormatHexLines(this IEnumerable<byte> bytes)
    {
        var sb = new StringBuilder(67);
        byte pos = 0;
        foreach (var b in bytes)
        {
            if (pos == 0)
            {
                sb.Append($"                        -                                          ");
            }

            var bStr = b.ToString("X2");
            if ((pos & 8) == 0)
            {
                sb[pos * 3] = bStr[0];
                sb[pos * 3 + 1] = bStr[1];
            }
            else
            {
                sb[2 + pos * 3] = bStr[0];
                sb[2 + pos * 3 + 1] = bStr[1];
            }

            sb[51 + pos] = char.IsControl((char)b) ? '.' : (char)b;

            pos++;
            pos &= 0xf;

            if (pos == 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

   public static IEnumerable<T> ToEnumerable<T>(this ReadOnlyMemory<T> span) => MemoryMarshal.ToEnumerable(span);

    public static T NullCheck<T>(this T obj, string param) where T : class => obj ?? throw new ArgumentNullException(param);


    public static IAsyncResult AsAsyncResult<T>(this Task<T> task, AsyncCallback callback, object state)
    {
        var returntask = task.ContinueWith((t, _) => t.Result, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    public static IAsyncResult AsAsyncResult(this Task task, AsyncCallback callback, object state)
    {
        var returntask = task.ContinueWith((t, _) => { }, state, TaskScheduler.Default);

        if (callback is not null)
        {
            returntask.ContinueWith(callback.Invoke, TaskScheduler.Default);
        }

        return returntask;
    }

    public static readonly Task<int> ZeroCompletedTask = Task.FromResult(0);


    public static void AddRange<T>(this List<T> list, params T[] collection) => list.AddRange(collection);

    public static SynchronizationContext GetSynchronizationContext(this ISynchronizeInvoke owner) =>
        owner.InvokeRequired ?
        owner.Invoke(new Func<SynchronizationContext>(() => SynchronizationContext.Current), null) as SynchronizationContext :
        SynchronizationContext.Current;


    public static WaitHandleAwaiter GetAwaiterWithTimeout(this WaitHandle handle, TimeSpan timeout) =>
        new(handle, timeout);

    public static WaitHandleAwaiter GetAwaiter(this WaitHandle handle) =>
        new(handle, Timeout.InfiniteTimeSpan);

    public static async Task<int> RunProcessAsync(string exe, string args)
    {
        using var ps = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = exe,
                Arguments = args
            }
        };

        ps.Start();

        return await ps;
    }

    public static ProcessAwaiter GetAwaiter(this Process process) =>
        new(process);

    public static unsafe Span<byte> GetSpan(IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    public static unsafe ReadOnlySpan<byte> GetReadOnlySpan(IntPtr ptr, int length) =>
        new(ptr.ToPointer(), length);

    public static unsafe Span<byte> GetSpan(this SafeBuffer ptr) =>
        new(ptr.DangerousGetHandle().ToPointer(), (int)ptr.ByteLength);

    public static unsafe ReadOnlySpan<byte> GetReadOnlySpan(this SafeBuffer ptr) =>
        new(ptr.DangerousGetHandle().ToPointer(), (int)ptr.ByteLength);



    public static WaitHandle CreateWaitHandle(this Process process, bool inheritable) =>
        NativeWaitHandle.DuplicateExisting(process.Handle, inheritable);

    private sealed class NativeWaitHandle : WaitHandle
    {
        [DllImport("kernel32")]
        private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32")]
        private static extern IntPtr GetCurrentProcess();

        public static NativeWaitHandle DuplicateExisting(IntPtr handle, bool inheritable)
        {
            if (!DuplicateHandle(GetCurrentProcess(), handle, GetCurrentProcess(), out var new_handle, 0, inheritable, 0x2))
            {
                throw new Win32Exception();
            }

            return new(new_handle);
        }

        public NativeWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }
}


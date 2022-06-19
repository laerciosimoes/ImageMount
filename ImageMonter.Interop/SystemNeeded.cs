using System.Runtime.InteropServices;
using ImageMounter.Interop.Enum;

namespace ImageMounter.Interop;

/// <summary>
/// Makes sure that screen stays on or computer does not go into sleep
/// during some work
/// </summary>
public class SystemNeeded : IDisposable
{


    [DllImport("KERNEL32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState executionState);

    [DllImport("KERNEL32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern uint GetCurrentThreadId();

    private readonly ExecutionState _previousState;

    private readonly uint _threadId;

    /// <summary>
    /// Initializes a block of code that is done with SystemRequired and Continous requirements
    /// </summary>
    public SystemNeeded() : this(ExecutionState.SystemRequired | ExecutionState.Continuous)
    { }

    /// <summary>
    /// Initializes a block of code that is done with certain resource and interface requirements
    /// </summary>
    public SystemNeeded(ExecutionState executionState)
    {
        _threadId = GetCurrentThreadId();
        _previousState = SetThreadExecutionState(executionState);
        if (_previousState == 0)
        {
            throw new Exception("SetThreadExecutionState failed.");
        }
    }

    /// <summary>
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_previousState != 0 && _threadId == GetCurrentThreadId())
        {
            SetThreadExecutionState(_previousState);
        }
    }

    /// <summary>
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

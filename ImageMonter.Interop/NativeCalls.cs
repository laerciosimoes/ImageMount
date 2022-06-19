using System.Reflection;
using System.Runtime.InteropServices;

namespace ImageMounter.Interop;

public static unsafe class NativeCalls
{

	static NativeCalls()
    {
        WindowsAPI.GetWindowsFunctions(out GenRandomBytesFunc, out GenRandomPtrFunc);
    }

	public static IntPtr CrtDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if ((libraryName.StartsWith("msvcr", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals("crtdll", StringComparison.OrdinalIgnoreCase)) &&
			!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return NativeLibrary.Load("c", assembly, searchPath);
        }

		return IntPtr.Zero;
	}

	private static class WindowsAPI
	{
		[DllImport("advapi32", CharSet = CharSet.Auto, EntryPoint = "SystemFunction036", SetLastError = true)]
        private static extern byte RtlGenRandom(IntPtr buffer, int length);

		[DllImport("advapi32", CharSet = CharSet.Auto, EntryPoint = "SystemFunction036", SetLastError = true)]
        private static extern byte RtlGenRandom(byte[] buffer, int length);

		public static void GetWindowsFunctions(out Action<byte[], int> GenRandomBytesFunc, out Action<IntPtr, int> GenRandomPtrFunc)
		{
			GenRandomBytesFunc = (buffer, length) => { if (RtlGenRandom(buffer, length) == 0) { throw new Exception("Random generation failed"); } };
			GenRandomPtrFunc = (buffer, length) => { if (RtlGenRandom(buffer, length) == 0) { throw new Exception("Random generation failed"); } };
		}
	}

	private static readonly Action<byte[], int> GenRandomBytesFunc;

	private static readonly Action<IntPtr, int> GenRandomPtrFunc;

	private static readonly Random Random = new();

	

	public static T GenRandomValue<T>() where T : unmanaged
	{
		T value;
		GenRandomPtrFunc(new IntPtr(&value), sizeof(T));
		return value;
	}

	public static sbyte GenRandomSByte() => GenRandomValue<sbyte>();

	public static short GenRandomInt16() => GenRandomValue<short>();

	public static int GenRandomInt32() => GenRandomValue<int>();

	public static long GenRandomInt64() => GenRandomValue<long>();

	public static byte GenRandomByte() => GenRandomValue<byte>();

	public static ushort GenRandomUInt16() => GenRandomValue<ushort>();

	public static uint GenRandomUInt32() => GenRandomValue<uint>();

	public static ulong GenRandomUInt64() => GenRandomValue<ulong>();

	public static Guid GenRandomGuid() => GenRandomValue<Guid>();

	public static byte[] GenRandomBytes(int count)
	{
		var bytes = new byte[count];
		GenRandomBytesFunc(bytes, count);
		return bytes;
	}

    public static uint GenerateDiskSignature() => GenRandomUInt32() | 0x80808081U & 0xFEFEFEFFU;

    public static void GenRandomBytes(byte[] bytes, int offset, int count) =>
		GenRandomBytes(bytes.AsSpan(offset, count));

    public static void GenRandomBytes(Span<byte> span)
    {
		fixed (byte* bytesPtr = span)
        {
			GenRandomPtrFunc(new IntPtr(bytesPtr), span.Length);
        }
    }
}

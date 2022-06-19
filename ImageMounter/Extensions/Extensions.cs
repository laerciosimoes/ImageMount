using System.Globalization;
using System.Reflection;
using System.Text;

namespace ImageMounter.Extensions
{
    public static class ExtensionMethods
    {
        public static string Join(this IEnumerable<string> strings, string separator)
        {
            return string.Join(separator, strings);
        }

        public static string Join(this string[] strings, string separator)
        {
            return string.Join(separator, strings);
        }

        public static string Concat(this IEnumerable<string> strings)
        {
            return string.Concat(strings);
        }

        public static string Concat(this string[] strings)
        {
            return string.Concat(strings);
        }

        public static void QueueDispose(this IDisposable instance)
        {
            ThreadPool.QueueUserWorkItem(() => instance.Dispose());
        }

        public static string ToMembersString(this object o)
        {
            if (o == null)
                return "{null}";
            else
                return typeof(System.Reflection.MembersStringParser<>).MakeGenericType(o.GetType()).GetMethod("ToString", BindingFlags.Public | BindingFlags.Static).Invoke(null, new[] { o }) as string;
        }

        public static string ToMembersString<T>(this T o) where T : struct
        {
            return System.Reflection.MembersStringParser<T>.ToString(o);
        }

        public static string ToHexString(this ICollection<byte> bytes, int offset, int count)
        {
            if (bytes == null || offset > bytes.Count || offset + count > bytes.Count)
                return null;

            StringBuilder valuestr = new StringBuilder(count << 1);
            for (var i = offset; i <= offset + count - 1; i++)
                valuestr.Append(bytes(i).ToString("x2"));

            return valuestr.ToString();
        }

        public static string ToHexString(this IEnumerable<byte> bytes)
        {
            if (bytes == null)
                return null;

            StringBuilder valuestr = new StringBuilder();
            foreach (var b in bytes)
                valuestr.Append(b.ToString("x2"));

            return valuestr.ToString();
        }

        public static byte[] ParseHexString(string str)
        {
 
            var bytes = new byte[str.Length ];

 
            for (var i = 0; i <= bytes.Length - 1; i++)

                /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
                bytes[i] = byte.Parse(str.Substring(i << 1, 2), NumberStyles.HexNumber);

            return bytes;
        }

        public static IEnumerable<byte> ParseHexString(IEnumerable<char> str)
        {
            
            var buffer = new char[1];
            foreach (var c in str)
            {
                if (buffer[0] == default(Char))
                    buffer[0] = c;
                else
                {
                    buffer[1] = c;
                    yield return byte.Parse(new string(buffer), NumberStyles.HexNumber);
                    Array.Clear(buffer, 0, 2);
                }
            }
        }

        public static byte[] ParseHexString(string str, int offset, int count)
        {
            
            var bytes = new byte[count ];

 
         
            
            for (var i = 0; i <= count - 1; i++)

                /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
                bytes[i] = byte.Parse(str.Substring((i + offset) << 1, 2), NumberStyles.HexNumber);

            return bytes;
        }
    }
}

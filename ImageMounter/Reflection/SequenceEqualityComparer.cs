using System.Runtime.InteropServices;

namespace ImageMounter.Reflection
{
    [ComVisible(false)]
    public sealed class SequenceEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public IEqualityComparer<T> ItemComparer { get; set; }

        public SequenceEqualityComparer(IEqualityComparer<T> comparer)
        {
            ItemComparer = comparer;
        }

        public SequenceEqualityComparer()
        {
            ItemComparer = EqualityComparer<T>.Default;
        }

        public new bool Equals(IEnumerable<T> x, IEnumerable<T> y)
        {
            return x.SequenceEqual(y, ItemComparer);
        }

        public new int GetHashCode(IEnumerable<T> obj)
        {
            int result;
            foreach (var item in obj)
                result = result ^ ItemComparer.GetHashCode(item);
            return result;
        }
    }

    [ComVisible(false)]
    public sealed class SequenceComparer<T> : IComparer<IEnumerable<T>>
    {
        public IComparer<T> ItemComparer { get; set; }

        public SequenceComparer(IComparer<T> comparer)
        {
            ItemComparer = comparer;
        }

        public SequenceComparer()
        {
            ItemComparer = Comparer<T>.Default;
        }

        public int Compare(IEnumerable<T> x, IEnumerable<T> y)
        {
            int value;
            using (var enumx = x.GetEnumerator())
            {
                using (var enumy = y.GetEnumerator())
                {
                    while (enumx.MoveNext() && enumy.MoveNext())
                    {
                        value = ItemComparer.Compare(enumx.Current, enumy.Current);
                        if (value != 0)
                            break;
                    }
                }
            }
            return value;
        }
    }
}
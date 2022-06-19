namespace ImageMounter.Reflection
{
    public sealed class EquatableBox<T> : IEquatable<T>, IEquatable<EquatableBox<T>> where T : struct, IEquatable<T>
    {
        public T Value { get; set; }

        public EquatableBox()
        {
        }

        public EquatableBox(T value)
        {
            Value = value;
        }

        public bool HasDefaultValue
        {
            get
            {
                return Value.Equals(new T());
            }
        }

        public void ClearValue()
        {
            Value = new T();
        }

        public static implicit operator EquatableBox<T>(T value)
        {
            return new EquatableBox<T>(value);
        }

        public static implicit operator T(EquatableBox<T> box)
        {
            return box.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public new bool Equals(EquatableBox<T> other)
        {
            return Value.Equals(other.Value);
        }

        public new bool Equals(T other)
        {
            return Value.Equals(other);
        }

        public new override bool Equals(object obj)
        {
            if (obj is EquatableBox<T>)
                return Value.Equals((EquatableBox<T>)obj.Value);
            else if (obj is T)
                return Value.Equals((T)obj);
            else
                return base.Equals(obj);
        }
    }
}
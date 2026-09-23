namespace Shop
{
    public class Result<T>
    {
        public Result(T value) => Value = value;

        public T Value { get; }
    }
}
